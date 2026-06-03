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

    /// <summary>Qué contenido muestra el panel lateral derecho ahora mismo (#153).</summary>
    private enum PanelMode { WorkEnv, Notes }
    private PanelMode _panelMode = PanelMode.WorkEnv;

    /// <summary>La ventana principal (única). La usan otras páginas p. ej. para "Salir".</summary>
    public static MainWindow? Current { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        Current = this;
        // Ruta ABSOLUTA: en apps empaquetadas el cwd no es la carpeta de la app, y una ruta
        // relativa puede no resolver (el icono de la ventana caería al logo por defecto). #icono
        try { AppWindow.SetIcon(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "AppIcon.ico")); }
        catch { AppWindow.SetIcon("Assets/AppIcon.ico"); }
        AppWindow.Closing += AppWindow_Closing;
        ApplyTheme(AppState.Load().ThemeMode);   // tema elegido por el usuario (#48)
    }

    /// <summary>Convierte el modo de tema guardado ("system"/"light"/"dark") en ElementTheme. #48</summary>
    public static ElementTheme ThemeFor(string? mode) => mode switch
    {
        "light" => ElementTheme.Light,
        "dark" => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    /// <summary>Aplica el tema en caliente a toda la ventana (la raíz es la NavigationView). #48</summary>
    public void ApplyTheme(string? mode)
    {
        if (Content is FrameworkElement fe) fe.RequestedTheme = ThemeFor(mode);
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exiting) return;          // salida real solicitada: dejar cerrar
        args.Cancel = true;            // cancelar el cierre…
        sender.Hide();                 // …y ocultar a segundo plano
        ScheduleHost.Instance.Start(); // re-asegura el servicio de avisos vivo en segundo plano
        TrayIconService.ShowBackgroundHintOnce();   // avisa (1 vez) de que sigue activo en la bandeja
    }

    /// <summary>
    /// Arranca en segundo plano sin robar el foco (autoarranque al iniciar sesión, #37):
    /// la ventana no se activa ni se muestra; solo corren los servicios de fondo (avisos).
    /// El usuario la abre cuando quiera (reaparece vía <see cref="ShowFromBackground"/>).
    /// </summary>
    public void StartInBackground()
    {
        // No llamamos Activate() (eso robaría el foco al iniciar sesión). Ocultamos por si
        // alguna versión de WinUI mostrara la ventana al crearse.
        try { AppWindow.Hide(); } catch { /* best-effort */ }
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
        TrayIconService.Dispose();     // quita el icono de la bandeja
        BlockStateServer.Stop();       // cierra el servidor del bloqueo (#8)
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
        UpdateWhatsNewBadge();   // "Novedades" se activa si la app se actualizó (#updates)

        // Primer arranque (#tutorial): tutorial guiado interactivo (reemplaza al selector de plantillas).
        // Corre en modo demo y, al final, ofrece conservar lo creado como plan inicial.
        if (AppState.IsFirstRun()) _ = RunTutorial(firstRun: true);
        // Verificación manual: forzar el tutorial en modo demo (replay) si existe
        // %USERPROFILE%\.ritmo\tutorial.flag — lo recorres sin que se persista NADA a tu config real.
        else if (System.IO.File.Exists(System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".ritmo", "tutorial.flag")))
            _ = RunTutorial(firstRun: false);
    }

    /// <summary>
    /// Muestra el onboarding del primer arranque, aplica la plantilla de categorías elegida
    /// y deja una fase inicial vacía para que el horario sea usable de inmediato (#83).
    /// </summary>
    private async System.Threading.Tasks.Task RunOnboarding()
    {
        var dlg = new Dialogs.OnboardingDialog { XamlRoot = Nav.XamlRoot };
        await dlg.ShowAsync();   // sin cancelar: por defecto la plantilla genérica

        AppState.Config.SeedTemplate(dlg.SelectedTemplate);   // siembra categorías + marca onboarding hecho
        // Una fase inicial vacía: sin ella el botón "Añadir" del horario está deshabilitado.
        if (AppState.Load().Plan.Phases.Count == 0)
            AppState.Config.AddPhase("Mi horario", System.DateOnly.FromDateTime(System.DateTime.Now), null);

        RebuildEnvNavItems();
        ContentFrame.Navigate(typeof(HomePage));   // refresca con las categorías ya sembradas
    }

    /// <summary>Muestra el aviso (badge) en «Novedades» si hay notas que el usuario no ha visto.</summary>
    private void UpdateWhatsNewBadge()
    {
        var pending = Ritmo.Core.Updates.ReleaseNotes.Since(AppState.Load().LastSeenVersion, AppVersionInfo.Current);
        WhatsNewBadge.Visibility = pending.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Abre el carrusel «Novedades» y marca esta versión como vista (apaga el badge).</summary>
    private async void ShowWhatsNew()
    {
        var current = AppVersionInfo.Current;
        var lang = Ritmo_App.Services.Loc.Lang;   // novedades en el idioma elegido (#48)
        var pending = Ritmo.Core.Updates.ReleaseNotes.Since(AppState.Load().LastSeenVersion, current, lang);
        var notes = pending.Count > 0
            ? pending
            : Ritmo.Core.Updates.ReleaseNotes.For(lang).Reverse().ToList();   // sin pendientes: navegar el histórico
        if (notes.Count == 0) return;

        var dlg = new Dialogs.WhatsNewDialog(notes) { XamlRoot = Nav.XamlRoot };
        await dlg.ShowAsync();

        AppState.Config.SetLastSeenVersion(current);
        WhatsNewBadge.Visibility = Visibility.Collapsed;
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
            "work" => typeof(WorkPage),
            "tasks" => typeof(TasksPage),
            "settings" => typeof(SettingsPage),
            "help" => typeof(HelpPage),
            "about" => typeof(AboutPage),
            _ => typeof(HomePage)
        };

        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page);
    }

    private void Nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var tag = args.InvokedItemContainer?.Tag as string;

        // "Novedades" no navega: abre el carrusel de novedades (#updates).
        if (tag == "whatsnew")
        {
            ShowWhatsNew();
        }
        // "Entornos de trabajo" no navega: abre/cierra el panel lateral derecho (#74).
        else if (tag == "workenv")
        {
            bool wasEnv = RightPanel.IsPaneOpen && _panelMode == PanelMode.WorkEnv;
            if (!wasEnv) BuildWorkEnvPanel();
            RightPanel.IsPaneOpen = !wasEnv;
        }
        // "Notas" no navega: abre/cierra el panel lateral derecho con las notas (#153).
        else if (tag == "notes")
        {
            bool wasNotes = RightPanel.IsPaneOpen && _panelMode == PanelMode.Notes;
            if (!wasNotes) BuildNotesPanel();
            RightPanel.IsPaneOpen = !wasNotes;
        }
        // Un sub-item de entorno: abre el panel enfocado en ese entorno (#102).
        else if (tag is not null && tag.StartsWith("env:"))
        {
            BuildWorkEnvPanel(tag["env:".Length..]);
            RightPanel.IsPaneOpen = true;
        }
    }

    /// <summary>#tutorial: referencia al botón "Nuevo entorno" del panel (se recrea en cada build).</summary>
    internal Microsoft.UI.Xaml.Controls.Button? TutorialNewEnvBtn;

    /// <summary>
    /// Rellena el panel derecho con cada entorno, sus enlaces y tareas (#74/#77).
    /// Si se pasa <paramref name="focusEnvId"/>, solo ese queda expandido y se enfoca.
    /// </summary>
    private void BuildWorkEnvPanel(string? focusEnvId = null)
    {
        _panelMode = PanelMode.WorkEnv;
        PanelTitle.Text = Loc.Pick("Entornos de trabajo", "Work environments");
        PanelSub.Text = Loc.Pick("Accesos rápidos por entorno.", "Quick shortcuts per environment.");
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
        TutorialNewEnvBtn = newBtn;   // #tutorial: handle para el recorte "Nuevo entorno"

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

    // ---------- Panel de notas (#153): acceso global rápido desde el navbar ----------

    /// <summary>Rellena el panel derecho con las notas, agrupadas: de hoy · generales · otras.</summary>
    private void BuildNotesPanel()
    {
        _panelMode = PanelMode.Notes;
        PanelTitle.Text = Loc.Pick("Notas", "Notes");
        PanelSub.Text = Loc.Pick("Acceso rápido a tus notas.", "Quick access to your notes.");
        WorkEnvPanel.Children.Clear();

        var settings = Services.AppState.Load();

        var newBtn = new Button { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 4) };
        newBtn.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center,
            Children = { new SymbolIcon(Symbol.Add), new TextBlock { Text = Loc.Pick("Nueva nota", "New note") } }
        };
        newBtn.Click += (_, _) => _ = AddNoteFromPanel();
        WorkEnvPanel.Children.Add(newBtn);

        if (settings.Notes.Count == 0)
        {
            WorkEnvPanel.Children.Add(new TextBlock
            {
                Text = Loc.Pick("Aún no tienes notas. Crea una para fijar recordatorios.",
                                "No notes yet. Create one to pin reminders."),
                Opacity = 0.6, FontSize = 13, TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        // Sesiones de hoy (título + categoría) para separar "de hoy" del resto.
        var today = System.DateOnly.FromDateTime(System.DateTime.Now);
        var phase = settings.Plan.GetActivePhase(today) ?? settings.Plan.OrderedPhases.FirstOrDefault();
        var schedule = phase?.Schedule ?? settings.Schedule;
        var todaySessions = schedule.Sessions.Where(s => s.Day == today.DayOfWeek).Select(s => (s.Title, s.CategoryId))
            .Concat(settings.OneOffSessions.Where(o => o.Date == today).Select(o => (o.Title, o.CategoryId)))
            .ToList();

        bool IsToday(Ritmo.Core.Model.StudyNote n) =>
            !n.IsGeneral && todaySessions.Any(s => n.AppliesTo(s.Title, s.CategoryId));

        var ordered = settings.Notes.OrderBy(n => n.Order).ToList();
        AddNoteGroup(Loc.Pick("DE HOY", "TODAY"), ordered.Where(IsToday), settings);
        AddNoteGroup(Loc.Pick("GENERALES", "GENERAL"), ordered.Where(n => n.IsGeneral), settings);
        AddNoteGroup(Loc.Pick("OTRAS", "OTHER"), ordered.Where(n => !n.IsGeneral && !IsToday(n)), settings);
    }

    private void AddNoteGroup(string header, System.Collections.Generic.IEnumerable<Ritmo.Core.Model.StudyNote> notes,
        Ritmo.Core.Persistence.AppSettings settings)
    {
        var list = notes.ToList();
        if (list.Count == 0) return;
        WorkEnvPanel.Children.Add(new TextBlock
        {
            Text = header, FontSize = 10, Opacity = 0.55, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 6, 0, 0)
        });
        foreach (var n in list) WorkEnvPanel.Children.Add(NotePanelRow(n, settings));
    }

    /// <summary>Una fila de nota en el panel: ámbito + título + editar/eliminar.</summary>
    private FrameworkElement NotePanelRow(Ritmo.Core.Model.StudyNote note, Ritmo.Core.Persistence.AppSettings settings)
    {
        var texts = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        string? scope = !string.IsNullOrEmpty(note.CategoryId)
            ? Loc.Pick("Categoría · ", "Category · ") + settings.CategoryName(note.CategoryId)
            : !string.IsNullOrEmpty(note.SessionTitle)
                ? Loc.Pick("Sesión · ", "Session · ") + note.SessionTitle
                : null;
        if (scope is not null)
            texts.Children.Add(new TextBlock { Text = scope, FontSize = 10, Opacity = 0.55,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        texts.Children.Add(new TextBlock { Text = note.Title, TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Grid.SetColumn(texts, 0);

        var edit = NoteRowIcon(Symbol.Edit, Loc.Pick("Editar nota", "Edit note"), () => _ = EditNoteFromPanel(note));
        Grid.SetColumn(edit, 1);
        var del = NoteRowIcon(Symbol.Delete, Loc.Pick("Eliminar nota", "Delete note"), () =>
        {
            Services.AppState.Config.RemoveNote(note.Id);
            BuildNotesPanel();
        });
        Grid.SetColumn(del, 2);

        var grid = new Grid { ColumnSpacing = 4, Padding = new Thickness(2, 4, 2, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(texts); grid.Children.Add(edit); grid.Children.Add(del);
        return grid;
    }

    private static Button NoteRowIcon(Symbol symbol, string tooltip, System.Action onClick)
    {
        var b = new Button
        {
            Content = new SymbolIcon(symbol) { Width = 16, Height = 16 },
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0), Padding = new Thickness(6), MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Top
        };
        ToolTipService.SetToolTip(b, tooltip);
        b.Click += (_, _) => onClick();
        return b;
    }

    private async System.Threading.Tasks.Task AddNoteFromPanel()
    {
        var dlg = new Dialogs.NoteDialog { XamlRoot = RightPanel.XamlRoot };
        dlg.EnableScope(Services.AppState.Load().Categories);   // #153: general / por categoría
        if (await dlg.ShowAsync() == ContentDialogResult.Primary && dlg.TitleText.Length > 0)
        {
            Services.AppState.Config.AddNote(dlg.TitleText, dlg.ContentText,
                sessionTitle: dlg.SelectedSessionTitle, categoryId: dlg.SelectedCategoryId);
            BuildNotesPanel();
        }
    }

    private async System.Threading.Tasks.Task EditNoteFromPanel(Ritmo.Core.Model.StudyNote note)
    {
        var dlg = new Dialogs.NoteDialog { XamlRoot = RightPanel.XamlRoot };
        dlg.EnableScope(Services.AppState.Load().Categories);
        dlg.LoadFrom(note);
        if (await dlg.ShowAsync() == ContentDialogResult.Primary && dlg.TitleText.Length > 0)
        {
            Services.AppState.Config.UpdateNote(note.Id, dlg.TitleText, dlg.ContentText,
                setScope: true, sessionTitle: dlg.SelectedSessionTitle, categoryId: dlg.SelectedCategoryId);
            BuildNotesPanel();
        }
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

        // --- Tareas (#145): conectadas con la funcionalidad «Tareas» (bloque vinculado al entorno) ---
        root.Children.Add(new TextBlock { Text = "TAREAS", FontSize = 10, Opacity = 0.55,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
        var st = Services.AppState.Load();
        var blk = st.TaskBlocks.FirstOrDefault(b => b.EnvironmentId == env.Id);
        int pend = blk is null ? 0 : st.Tasks.Count(t => t.BlockId == blk.Id && !t.Done);
        root.Children.Add(new TextBlock
        {
            Text = blk is null ? "Sin lista vinculada todavía." : (pend == 0 ? "Sin tareas pendientes." : $"{pend} pendientes."),
            Opacity = 0.6, FontSize = 12
        });
        var openTasksBtn = new Button { Content = "Ver tareas", Margin = new Thickness(0, 4, 0, 0) };
        openTasksBtn.Click += (_, _) =>
        {
            var r = Services.AppState.Config.EnsureEnvironmentTaskBlock(env.Id, env.Name);
            TasksPage.PendingBlockId = r.Success ? r.Message : null;
            foreach (var mi in Nav.MenuItems.OfType<NavigationViewItem>())
                if ((string?)mi.Tag == "tasks") { Nav.SelectedItem = mi; break; }
            RightPanel.IsPaneOpen = false;
        };
        root.Children.Add(openTasksBtn);

        // El seguimiento laboral (#84) se movió del panel del entorno a su propia página
        // «Trabajo» (#84 V3), porque ahora los proyectos son un concepto independiente.

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
