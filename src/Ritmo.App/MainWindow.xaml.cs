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

    private void Nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        // "Entornos de trabajo" no navega: abre/cierra el panel lateral derecho (#74).
        if (args.InvokedItemContainer?.Tag as string == "workenv")
        {
            if (!RightPanel.IsPaneOpen) BuildWorkEnvPanel();
            RightPanel.IsPaneOpen = !RightPanel.IsPaneOpen;
        }
    }

    /// <summary>Rellena el panel derecho con cada entorno y sus enlaces (#74).</summary>
    private void BuildWorkEnvPanel()
    {
        WorkEnvPanel.Children.Clear();
        var envs = Services.AppState.Load().FocusEnvironments;
        if (envs.Count == 0)
        {
            WorkEnvPanel.Children.Add(new TextBlock
            {
                Text = "Aún no hay entornos. Créalos en Ajustes y añádeles enlaces.",
                Opacity = 0.6, FontSize = 13, TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var env in envs)
        {
            var links = new StackPanel { Spacing = 2 };
            if (env.Links.Count == 0)
                links.Children.Add(new TextBlock { Text = "Sin enlaces", Opacity = 0.5, FontSize = 12 });
            else
                foreach (var l in env.Links)
                {
                    var btn = new HyperlinkButton { Content = l.Title, Padding = new Thickness(0, 2, 0, 2) };
                    ToolTipService.SetToolTip(btn, l.Url);
                    var url = l.Url;
                    btn.Click += (_, _) => OpenUrl(url);
                    links.Children.Add(btn);
                }

            WorkEnvPanel.Children.Add(new Expander
            {
                Header = env.Name,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsExpanded = true,
                Content = links
            });
        }
    }

    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch { }
    }
}
