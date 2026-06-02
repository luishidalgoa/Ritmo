using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Ritmo_App.Services;

namespace Ritmo_App;

/// <summary>
/// Aplicación. Es de instancia única (#24/#20): si ya hay una corriendo, la
/// segunda activación se redirige a la primera (que reaparece) y la segunda se
/// cierra. Al cerrar la ventana la app NO sale: se oculta y sigue viva en segundo
/// plano para que los avisos del horario suenen igualmente. Se sale del todo con
/// la acción "Salir" de Ajustes.
/// </summary>
public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        ApplyLanguageOverride();   // idioma elegido (#48), ANTES de cargar la UI
        InitializeComponent();
    }

    /// <summary>Aplica el idioma elegido (#48 i18n) al sistema de recursos antes de crear la UI.</summary>
    private static void ApplyLanguageOverride()
    {
        try
        {
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = AppState.Load().Language switch
            {
                "es" => "es-ES",
                "en" => "en-US",
                _ => ""   // "system": sin override, sigue a Windows
            };
        }
        catch { /* si falla, arranca con el idioma del sistema */ }
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var main = AppInstance.FindOrRegisterForKey("ritmo-single-instance");
        if (!main.IsCurrent)
        {
            // Ya hay una instancia: redirige la activación a ella y cierra esta.
            RedirectAndExit(main);
            return;
        }

        // Soy la instancia principal: atiendo futuras (re)activaciones.
        main.Activated += OnReactivated;

        // Núcleo centralizado de notificaciones (#128): registra los canales una vez.
        // Cualquier emisor (horario, etc.) llama a NotificationHub.Notify y el hub reparte.
        NotificationHub.Instance.Register(new ToastChannel());
        NotificationHub.Instance.Register(new NtfyChannel());

        // Servicio de fondo: vigila el horario y lanza toasts (#28/#29). Sigue vivo
        // aunque se oculte la ventana; solo se para al salir de verdad.
        ScheduleHost.Instance.Start();

        // Servidor local (loopback) que publica el estado del bloqueo para la extensión de
        // navegador (#8, bloqueo duro a nivel de red). Best-effort; si falla, el blando cubre.
        BlockStateServer.Start();

        // Re-planifica los avisos cada vez que cambian los ajustes (añadir/editar sesión,
        // edición por la IA vía MCP, importar…). Antes solo se planificaba al arrancar, así
        // que una sesión nueva no avisaba hasta reiniciar. #128
        AppState.SettingsChanged += () => ScheduleHost.Instance.Start();

        // Auto-sincroniza tareas con Google Tasks 15 s después de detectar un cambio en una lista
        // vinculada (debounce). Solo si hay conexión; lo local no lo dispara. #64
        Services.TaskAutoSync.Instance.Start();

        _window = new MainWindow();

        // Icono de bandeja: hace visible que Ritmo sigue activo en segundo plano (menú Abrir/Salir).
        Services.TrayIconService.Setup(_window);

        // Autoarranque al iniciar sesión (#37): si nos lanzó la tarea de inicio de Windows,
        // arrancamos EN SEGUNDO PLANO (sin robar foco ni abrir la ventana). Los avisos del
        // horario corren igual porque ScheduleHost es de nivel app. El usuario abre la ventana
        // cuando quiera desde Inicio (reactivación → ShowFromBackground).
        if (LaunchedAtStartup())
            _window.StartInBackground();
        else
            _window.Activate();
    }

    /// <summary>¿Nos ha lanzado la tarea de arranque automático al iniciar sesión? #37</summary>
    private static bool LaunchedAtStartup()
    {
        try
        {
            return AppInstance.GetCurrent().GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask;
        }
        catch { return false; }
    }

    private static async void RedirectAndExit(AppInstance main)
    {
        try
        {
            var aea = AppInstance.GetCurrent().GetActivatedEventArgs();
            await main.RedirectActivationToAsync(aea);
        }
        catch { /* best-effort */ }
        // Cierra esta instancia secundaria; la principal ya recibió la activación.
        Process.GetCurrentProcess().Kill();
    }

    private void OnReactivated(object? sender, AppActivationArguments e)
    {
        // El evento llega en un hilo de fondo: vuelve al hilo de UI de la ventana.
        var w = _window;
        w?.DispatcherQueue.TryEnqueue(() => w.ShowFromBackground());
    }
}
