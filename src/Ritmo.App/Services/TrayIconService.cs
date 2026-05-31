using System;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;

namespace Ritmo_App.Services;

/// <summary>
/// Icono de bandeja del sistema. Hace VISIBLE que Ritmo sigue activo en segundo plano
/// (al cerrar la ventana, el proceso no muere: sus avisos del horario siguen sonando, #128).
/// Doble clic = abrir; menú contextual = Abrir / Salir de Ritmo.
/// </summary>
internal static class TrayIconService
{
    private static TaskbarIcon? _icon;
    private static bool _hintShown;

    /// <summary>Crea el icono de bandeja (una sola vez), enlazado a la ventana principal.</summary>
    public static void Setup(MainWindow window)
    {
        if (_icon is not null) return;
        try { SetupCore(window); }
        catch { _icon = null; /* un fallo de la bandeja NUNCA debe impedir arrancar la app */ }
    }

    private static void SetupCore(MainWindow window)
    {
        var open = new MenuFlyoutItem { Text = "Abrir Ritmo" };
        open.Click += (_, _) => window.ShowFromBackground();
        var exit = new MenuFlyoutItem { Text = "Salir de Ritmo" };
        exit.Click += (_, _) => window.ExitApp();
        var menu = new MenuFlyout();
        menu.Items.Add(open);
        menu.Items.Add(exit);

        _icon = new TaskbarIcon
        {
            ToolTipText = "Ritmo — activo en segundo plano",
            ContextFlyout = menu,
        };
        try { _icon.Icon = new System.Drawing.Icon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico")); }
        catch { /* sin icono: la bandeja muestra uno por defecto */ }
        _icon.LeftClickCommand = new RelayCommand(window.ShowFromBackground);   // clic en el icono = abrir
        _icon.ForceCreate();
    }

    /// <summary>ICommand mínimo para enlazar acciones del icono de bandeja.</summary>
    private sealed class RelayCommand(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
    }

    /// <summary>Avisa (UNA vez) de que la app sigue en segundo plano al cerrar la ventana.</summary>
    public static void ShowBackgroundHintOnce()
    {
        if (_hintShown || _icon is null) return;
        _hintShown = true;
        try
        {
            _icon.ShowNotification(
                "Ritmo sigue activo",
                "Sigue en segundo plano para tus avisos. Haz clic en su icono de la bandeja para abrirlo, o botón derecho → Salir de Ritmo.");
        }
        catch { /* el aviso es best-effort */ }
    }

    /// <summary>Quita el icono de la bandeja (al salir de verdad).</summary>
    public static void Dispose()
    {
        try { _icon?.Dispose(); } catch { }
        _icon = null;
    }
}
