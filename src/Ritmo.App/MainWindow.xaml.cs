using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo_App.Services;

namespace Ritmo_App;

/// <summary>
/// Ventana principal: barra de título + NavigationView que conmuta entre las
/// páginas (Hoy / Temporizador / Horario / Ajustes) dentro de un Frame.
///
/// Al cerrar, NO sale de la app: se oculta y sigue en segundo plano (#24/#20),
/// para que los avisos del horario sigan sonando. Se sale del todo con ExitApp().
/// </summary>
public sealed partial class MainWindow : Window
{
    private bool _exiting;

    /// <summary>La ventana principal (única). La usan otras páginas p. ej. para "Salir".</summary>
    public static MainWindow? Current { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        Current = this;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Closing += AppWindow_Closing;
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exiting) return;          // salida real solicitada: dejar cerrar
        args.Cancel = true;            // cancelar el cierre…
        sender.Hide();                 // …y ocultar a segundo plano
    }

    /// <summary>Reaparece desde segundo plano (al reactivar la app o pulsar abrir).</summary>
    public void ShowFromBackground()
    {
        AppWindow.Show();
        AppWindow.MoveInZOrderAtTop();
        Activate();
    }

    /// <summary>Sale de Ritmo del todo: para el servicio de avisos y cierra el proceso.</summary>
    public void ExitApp()
    {
        _exiting = true;
        ScheduleHost.Instance.Stop();
        ToastService.Unregister();
        Close();
        Application.Current.Exit();
    }

    private void Nav_Loaded(object sender, RoutedEventArgs e)
    {
        // Página inicial: Hoy.
        ContentFrame.Navigate(typeof(HomePage));
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;

        var page = item.Tag switch
        {
            "home" => typeof(HomePage),
            "timer" => typeof(TimerPage),
            "schedule" => typeof(SchedulePage),
            "settings" => typeof(SettingsPage),
            _ => typeof(HomePage)
        };

        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page);
    }
}
