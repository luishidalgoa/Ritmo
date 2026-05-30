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
        RebuildEnvNavItems();
    }

    /// <summary>
    /// Llena "Entornos de trabajo" con un sub-item por entorno, de modo que el botón
    /// sea desplegable y muestre los disponibles. Invocar uno abre el panel en él.
    /// </summary>
    private void RebuildEnvNavItems()
    {
        WorkEnvNav.MenuItems.Clear();
        var settings = Services.AppState.Load();
        foreach (var env in settings.FocusEnvironments)
        {
            WorkEnvNav.MenuItems.Add(new NavigationViewItem
            {
                Tag = $"env:{env.Id}",
                SelectsOnInvoked = false,
                Icon = new SymbolIcon(Symbol.Tag),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = EnvNavContent(env, env.Id == settings.DefaultFocusEnvironmentId)
            });
        }
    }

    /// <summary>Contenido de un sub-item de entorno: nombre + "Seleccionar" o marca de activo (#104).</summary>
    private FrameworkElement EnvNavContent(Ritmo.Core.Focus.FocusEnvironment env, bool isSelected)
    {
        var name = new TextBlock
        {
            Text = env.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(name, 0);

        FrameworkElement trailing;
        if (isSelected)
        {
            var check = new SymbolIcon(Symbol.Accept)
            {
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
            };
            ToolTipService.SetToolTip(check, "Entorno activo");
            trailing = check;
        }
        else
        {
            var sel = new Button
            {
                Content = "Seleccionar",
                FontSize = 12,
                Padding = new Thickness(8, 2, 8, 2)
            };
            sel.Click += (_, _) =>
            {
                Services.AppState.Config.SetDefaultEnvironment(env.Id);
                RebuildEnvNavItems();
            };
            trailing = sel;
        }
        Grid.SetColumn(trailing, 1);

        var grid = new Grid { ColumnSpacing = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(name);
        grid.Children.Add(trailing);
        return grid;
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
            "help" => typeof(HelpPage),
            _ => typeof(HomePage)
        };

        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page);
    }

    private void Nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var tag = args.InvokedItemContainer?.Tag as string;

        // "Entornos de trabajo" no navega: abre/cierra el panel lateral derecho (#74).
        if (tag == "workenv")
        {
            if (!RightPanel.IsPaneOpen) BuildWorkEnvPanel();
            RightPanel.IsPaneOpen = !RightPanel.IsPaneOpen;
        }
        // Un sub-item de entorno: abre el panel enfocado en ese entorno (#102).
        else if (tag is not null && tag.StartsWith("env:"))
        {
            BuildWorkEnvPanel(tag["env:".Length..]);
            RightPanel.IsPaneOpen = true;
        }
    }

    /// <summary>
    /// Rellena el panel derecho con cada entorno, sus enlaces y tareas (#74/#77).
    /// Si se pasa <paramref name="focusEnvId"/>, solo ese queda expandido y se enfoca.
    /// </summary>
    private void BuildWorkEnvPanel(string? focusEnvId = null)
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

        Expander? focused = null;
        foreach (var env in envs)
        {
            var exp = new Expander
            {
                Header = env.Name,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsExpanded = focusEnvId is null || env.Id == focusEnvId,
                Content = BuildEnvContent(env)
            };
            if (env.Id == focusEnvId) focused = exp;
            WorkEnvPanel.Children.Add(exp);
        }
        focused?.StartBringIntoView();
    }

    private StackPanel BuildEnvContent(Ritmo.Core.Focus.FocusEnvironment env)
    {
        var root = new StackPanel { Spacing = 10 };

        // --- Acciones del entorno: concentrarse (#111) / editar / eliminar (#102) ---
        var focusBtn = new Button
        {
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6,
                Children = { new SymbolIcon(Symbol.Play), new TextBlock { Text = "Concentrarse" } }
            }
        };
        focusBtn.Click += (_, _) => ConcentrateWith(env);

        var editBtn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6,
                Children = { new SymbolIcon(Symbol.Edit), new TextBlock { Text = "Editar" } }
            }
        };
        editBtn.Click += (_, _) => _ = EditEnvironment(env);

        var delBtn = new Button
        {
            Content = new SymbolIcon(Symbol.Delete),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0)
        };
        ToolTipService.SetToolTip(delBtn, "Eliminar entorno");
        delBtn.Click += (_, _) => _ = DeleteEnvironment(env);

        root.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6,
            Children = { focusBtn, editBtn, delBtn }
        });

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
            var env = dlg.ToEnvironment();
            Services.AppState.Config.UpsertEnvironment(env);
            RebuildEnvNavItems();
            BuildWorkEnvPanel(env.Id);
        }
    }

    /// <summary>Selecciona el entorno y arranca el temporizador (cockpit, #111).</summary>
    private void ConcentrateWith(Ritmo.Core.Focus.FocusEnvironment env)
    {
        Services.AppState.Config.SetDefaultEnvironment(env.Id);
        RebuildEnvNavItems();
        RightPanel.IsPaneOpen = false;
        TimerPage.AutoStartPending = true;
        foreach (var it in Nav.MenuItems.OfType<NavigationViewItem>())
            if (it.Tag as string == "timer") { Nav.SelectedItem = it; break; }
    }

    /// <summary>Edita un entorno desde el panel derecho (#102).</summary>
    private async System.Threading.Tasks.Task EditEnvironment(Ritmo.Core.Focus.FocusEnvironment env)
    {
        var dlg = new Dialogs.EnvironmentDialog { XamlRoot = RightPanel.XamlRoot };
        dlg.LoadFrom(env);
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            Services.AppState.Config.UpsertEnvironment(dlg.ToEnvironment());
            RebuildEnvNavItems();
            BuildWorkEnvPanel(env.Id);
        }
    }

    /// <summary>Elimina un entorno desde el panel derecho, con confirmación (#102).</summary>
    private async System.Threading.Tasks.Task DeleteEnvironment(Ritmo.Core.Focus.FocusEnvironment env)
    {
        var confirm = new ContentDialog
        {
            XamlRoot = RightPanel.XamlRoot,
            Title = "Eliminar entorno",
            Content = $"¿Eliminar «{env.Name}»? Esta acción no se puede deshacer.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            Services.AppState.Config.RemoveEnvironment(env.Id);
            RebuildEnvNavItems();
            BuildWorkEnvPanel();
        }
    }

    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch { }
    }
}
