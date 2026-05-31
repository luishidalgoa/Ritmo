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
        InitializeComponent();
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

        // Re-planifica los avisos cada vez que cambian los ajustes (añadir/editar sesión,
        // edición por la IA vía MCP, importar…). Antes solo se planificaba al arrancar, así
        // que una sesión nueva no avisaba hasta reiniciar. #128
        AppState.SettingsChanged += () => ScheduleHost.Instance.Start();

        _window = new MainWindow();
        _window.Activate();
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
