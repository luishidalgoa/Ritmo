using System.Linq;
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

    /// <summary>Rellena el panel derecho con cada entorno, sus enlaces y tareas (#74/#77).</summary>
    private void BuildWorkEnvPanel()
    {
        WorkEnvPanel.Children.Clear();

        // Botón para crear un entorno desde aquí mismo (#92).
        var newBtn = new Button { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 4) };
        newBtn.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center,
            Children = { new SymbolIcon(Symbol.Add), new TextBlock { Text = "Nuevo entorno" } }
        };
        newBtn.Click += (_, _) => _ = NewEnvironment();
        WorkEnvPanel.Children.Add(newBtn);

        var envs = Services.AppState.Load().FocusEnvironments;
        if (envs.Count == 0)
        {
            WorkEnvPanel.Children.Add(new TextBlock
            {
                Text = "Aún no hay entornos. Crea uno para agrupar tu música/apps, enlaces y tareas.",
                Opacity = 0.6, FontSize = 13, TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var env in envs)
        {
            WorkEnvPanel.Children.Add(new Expander
            {
                Header = env.Name,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsExpanded = true,
                Content = BuildEnvContent(env)
            });
        }
    }

    private StackPanel BuildEnvContent(Ritmo.Core.Focus.FocusEnvironment env)
    {
        var root = new StackPanel { Spacing = 10 };

        // --- Enlaces ---
        root.Children.Add(new TextBlock { Text = "ENLACES", FontSize = 10, Opacity = 0.55,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        if (env.Links.Count == 0)
            root.Children.Add(new TextBlock { Text = "Sin enlaces", Opacity = 0.5, FontSize = 12 });
        foreach (var l in env.Links)
        {
            var btn = new HyperlinkButton { Content = l.Title, Padding = new Thickness(0, 2, 0, 2) };
            ToolTipService.SetToolTip(btn, l.Url);
            var url = l.Url;
            btn.Click += (_, _) => OpenUrl(url);
            root.Children.Add(btn);
        }

        // --- Tareas (#77) ---
        root.Children.Add(new TextBlock { Text = "TAREAS", FontSize = 10, Opacity = 0.55,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
        foreach (var task in env.Tasks.OrderBy(t => t.Done).ThenBy(t => t.Order))
            root.Children.Add(TaskRow(env.Id, task));

        var addBox = new TextBox { PlaceholderText = "Nueva tarea…", FontSize = 13 };
        var addBtn = new Button { Content = new SymbolIcon(Symbol.Add), Padding = new Thickness(6) };
        void AddTask()
        {
            if (string.IsNullOrWhiteSpace(addBox.Text)) return;
            Services.AppState.Config.AddEnvironmentTask(env.Id, addBox.Text);
            BuildWorkEnvPanel();
        }
        addBtn.Click += (_, _) => AddTask();
        addBox.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) AddTask(); };
        var addRow = new Grid { ColumnSpacing = 4, Margin = new Thickness(0, 2, 0, 0) };
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(addBox, 0); Grid.SetColumn(addBtn, 1);
        addRow.Children.Add(addBox); addRow.Children.Add(addBtn);
        root.Children.Add(addRow);

        return root;
    }

    private FrameworkElement TaskRow(string envId, Ritmo.Core.Focus.EnvironmentTask task)
    {
        var txt = new TextBlock { Text = task.Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        if (task.Done) { txt.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough; txt.Opacity = 0.55; }

        var chk = new CheckBox { IsChecked = task.Done, Content = txt, MinWidth = 0 };
        chk.Click += (_, _) => { Services.AppState.Config.ToggleEnvironmentTask(envId, task.Id); BuildWorkEnvPanel(); };
        Grid.SetColumn(chk, 0);

        var del = new Button { Content = new SymbolIcon(Symbol.Delete), Padding = new Thickness(6),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
        ToolTipService.SetToolTip(del, "Eliminar tarea");
        del.Click += (_, _) => { Services.AppState.Config.RemoveEnvironmentTask(envId, task.Id); BuildWorkEnvPanel(); };
        Grid.SetColumn(del, 1);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(chk); grid.Children.Add(del);
        return grid;
    }

    private async System.Threading.Tasks.Task NewEnvironment()
    {
        var dlg = new Dialogs.EnvironmentDialog { XamlRoot = RightPanel.XamlRoot };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            Services.AppState.Config.UpsertEnvironment(dlg.ToEnvironment());
            BuildWorkEnvPanel();
        }
    }

    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch { }
    }
}
