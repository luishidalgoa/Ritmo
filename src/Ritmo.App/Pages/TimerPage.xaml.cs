using System;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo.Core.Pomodoro;
using Ritmo.Core.Scheduling;
using Ritmo.Core.Timing;
using Ritmo_App.Services;

namespace Ritmo_App;

/// <summary>
/// Vista del temporizador Pomodoro: refleja en pantalla el PomodoroEngine del
/// núcleo. Un DispatcherQueueTimer refresca la UI; el motor recibe el "ahora"
/// del SystemClock (la lógica vive en el núcleo, ya testeada).
///
/// Además conecta el temporizador con el horario (#67): si ahora toca un bloque
/// del plan, muestra su título, usa el preset Pomodoro del entorno mapeado a
/// su categoría y, al concentrar, aplica ese entorno (no el genérico por defecto).
/// </summary>
public sealed partial class TimerPage : Page
{
    private readonly IClock _clock = new SystemClock();
    private PomodoroEngine _engine = new(PomodoroConfig.DeepWork);
    private readonly IFocusController _focus = new WindowsFocusController();
    private DispatcherQueueTimer? _ticker;
    private bool _environmentApplied;
    private bool _createdDesktop;   // #110: creamos un escritorio virtual esta sesión
    // Espera tras crear el escritorio virtual antes de lanzar apps/navegador, para que el cambio
    // de escritorio (asíncrono) se complete y se abran EN el escritorio nuevo (#141).
    private const int WorkspaceLaunchDelayMs = 700;

    // Contexto del bloque vigente (resuelto desde el horario).
    private FocusEnvironment? _activeEnv;
    private string? _activeSessionTitle;   // título de la sesión activa (perfil por tipo, #116)

    // Isla flotante de concentración (#118).
    private FocusOverlayWindow? _overlay;
    private bool _compact;

    /// <summary>
    /// Si otra pantalla (Hoy / Horario) pide "empezar ya", lo deja marcado aquí;
    /// el Timer lo consume al cargarse y arranca la concentración automáticamente.
    /// </summary>
    public static bool AutoStartPending { get; set; }

    public TimerPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => { _ticker?.Stop(); _focus.Exit(); TaskbarSilencer.Restore(); _overlay?.Close(); _overlay = null; };
    }

    private bool _loadingEnv;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ResolveContext();
        BuildEnvSelector(AppState.Load());
        BuildNotes();   // notas de la sesión activa, a mano (#151)

        _ticker = DispatcherQueue.CreateTimer();
        _ticker.Interval = TimeSpan.FromMilliseconds(250);
        _ticker.Tick += (_, _) => Refresh();

        if (AutoStartPending)
        {
            AutoStartPending = false;
            StartBtn_Click(this, new RoutedEventArgs());   // arranca la concentración ya
        }
        else
        {
            Refresh();
        }
    }

    // ---------- Notas de la sesión activa, a mano en concentración (#151) ----------

    /// <summary>Pinta, en el desplegable de notas, las de la SESIÓN ACTIVA (por su título). #151</summary>
    private void BuildNotes()
    {
        NotesHost.Children.Clear();
        var title = _activeSessionTitle;
        if (string.IsNullOrWhiteSpace(title))
        {
            NotesExpander.Visibility = Visibility.Collapsed;   // sin bloque ahora: no hay notas por tipo
            return;
        }

        var notes = AppState.Load().Notes
            .Where(n => string.Equals(n.SessionTitle, title, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Order).ToList();

        NotesExpander.Header = notes.Count > 0 ? $"Notas · {title} ({notes.Count})" : $"Notas · {title}";
        NotesExpander.Visibility = Visibility.Visible;

        if (notes.Count == 0)
            NotesHost.Children.Add(new TextBlock { Text = "Sin notas para esta sesión.", Opacity = 0.55, FontSize = 12 });
        foreach (var note in notes) NotesHost.Children.Add(NoteCard(note));

        var add = new HyperlinkButton { Content = "+ Añadir nota", Padding = new Thickness(0) };
        add.Click += async (_, _) => await AddNote(title);
        NotesHost.Children.Add(add);
    }

    private FrameworkElement NoteCard(StudyNote note)
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var t = new TextBlock { Text = note.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(t, 0);
        var edit = NoteIconButton(Symbol.Edit, "Editar nota", () => _ = EditNote(note));
        Grid.SetColumn(edit, 1);
        var del = NoteIconButton(Symbol.Delete, "Eliminar nota", () => RemoveNote(note));
        Grid.SetColumn(del, 2);
        header.Children.Add(t); header.Children.Add(edit); header.Children.Add(del);

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(header);
        if (!string.IsNullOrWhiteSpace(note.Content))
        {
            var md = MarkdownRenderer.Build(note.Content);
            md.Opacity = 0.85;
            stack.Children.Add(md);
        }
        return new Border
        {
            Padding = new Thickness(10, 8, 6, 8), CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], BorderThickness = new Thickness(1),
            Child = stack
        };
    }

    private static Button NoteIconButton(Symbol symbol, string tooltip, Action onClick)
    {
        var b = new Button
        {
            Content = new SymbolIcon(symbol),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0), Padding = new Thickness(6, 2, 6, 2),
            MinWidth = 0, VerticalAlignment = VerticalAlignment.Top
        };
        ToolTipService.SetToolTip(b, tooltip);
        b.Click += (_, _) => onClick();
        return b;
    }

    private async System.Threading.Tasks.Task AddNote(string sessionTitle)
    {
        var dlg = new Ritmo_App.Dialogs.NoteDialog { XamlRoot = this.XamlRoot };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary && dlg.TitleText.Length > 0)
        {
            AppState.Config.AddNote(dlg.TitleText, dlg.ContentText, sessionTitle: sessionTitle);
            BuildNotes();
        }
    }

    private async System.Threading.Tasks.Task EditNote(StudyNote note)
    {
        var dlg = new Ritmo_App.Dialogs.NoteDialog { XamlRoot = this.XamlRoot };
        dlg.LoadFrom(note);
        if (await dlg.ShowAsync() == ContentDialogResult.Primary && dlg.TitleText.Length > 0)
        {
            AppState.Config.UpdateNote(note.Id, dlg.TitleText, dlg.ContentText);
            BuildNotes();
        }
    }

    private void RemoveNote(StudyNote note)
    {
        AppState.Config.RemoveNote(note.Id);
        BuildNotes();
    }

    /// <summary>
    /// Mira el horario: si hay un bloque activo AHORA, carga su título, el
    /// entorno mapeado a su categoría y el preset Pomodoro de ese entorno. Si no hay
    /// bloque, usa el Pomodoro de ajustes ("concentración libre"). Solo recompone
    /// el motor cuando está en Idle, para no interrumpir una sesión en curso.
    /// </summary>
    private void ResolveContext()
    {
        var settings = AppState.Load();
        var now = _clock.Now;
        var today = DateOnly.FromDateTime(now);
        var phase = settings.Plan.GetActivePhase(today) ?? settings.Plan.OrderedPhases.FirstOrDefault();
        var schedule = phase?.Schedule ?? settings.Schedule;
        // Una sesión provisional que cubre AHORA tiene prioridad sobre el horario (#103).
        var oneOff = OneOffPlanner.ActiveAt(settings.OneOffSessions, now);
        var active = oneOff?.AsSession() ?? new SchedulePlanner(schedule, settings.FocusCategoryIds()).GetActiveSession(now);
        _activeSessionTitle = active?.Title;   // tipo de sesión para resolver qué abrir (#116)

        PomodoroConfig config;
        if (active is not null)
        {
            _activeEnv = settings.ResolveEnvironment(active.CategoryId);
            config = PomodoroRhythms.Resolve(_activeEnv?.PomodoroPreset, settings.Rhythms, settings.Pomodoro);
            SubjectText.Text = active.Title;
            SubjectMeta.Text = $"{settings.CategoryName(active.CategoryId)} · {active.Start:HH\\:mm}–{active.End:HH\\:mm}";
        }
        else
        {
            _activeEnv = settings.ResolveEnvironment(Ritmo.Core.Model.CategoryIds.Other);   // el entorno seleccionado/por defecto
            config = _activeEnv is not null
                ? PomodoroRhythms.Resolve(_activeEnv.PomodoroPreset, settings.Rhythms, settings.Pomodoro)
                : settings.Pomodoro;
            SubjectText.Text = "Sin bloque ahora";
            SubjectMeta.Text = _activeEnv is not null ? $"Concentración libre · {_activeEnv.Name}" : "Concentración libre";
        }
        SubjectBadge.Visibility = Visibility.Visible;

        if (_engine.Phase == PomodoroPhase.Idle)
            _engine = new PomodoroEngine(config);

        PresetInfo.Text =
            $"Concentración {config.Focus.TotalMinutes:0} · descanso {config.ShortBreak.TotalMinutes:0} · " +
            $"largo {config.LongBreak.TotalMinutes:0} cada {config.FocusesPerLongBreak} focos";
    }

    /// <summary>Llena el desplegable de entorno activo: "Automático" + cada entorno (#104).</summary>
    private void BuildEnvSelector(Ritmo.Core.Persistence.AppSettings settings)
    {
        _loadingEnv = true;
        EnvBox.Items.Clear();
        EnvBox.Items.Add(new ComboBoxItem { Content = "Automático (según el bloque)", Tag = "" });
        foreach (var env in settings.FocusEnvironments)
            EnvBox.Items.Add(new ComboBoxItem { Content = env.Name, Tag = env.Id });

        var sel = settings.DefaultFocusEnvironmentId ?? "";
        int idx = 0;
        for (int i = 0; i < EnvBox.Items.Count; i++)
            if (EnvBox.Items[i] is ComboBoxItem it && (string)it.Tag == sel) { idx = i; break; }
        EnvBox.SelectedIndex = idx;
        EnvBox.Visibility = settings.FocusEnvironments.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _loadingEnv = false;
    }

    private void EnvBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingEnv) return;
        var id = (EnvBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        AppState.Config.SetDefaultEnvironment(string.IsNullOrEmpty(id) ? null : id);
        if (_engine.Phase == PomodoroPhase.Idle) ResolveContext();   // refresca preset/entorno si no hay sesión en curso
        Refresh();
    }

    private async void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        bool wasIdle = _engine.Phase == PomodoroPhase.Idle;
        if (wasIdle)
        {
            // Vista previa del día al arrancar concentración, si está activada (#47).
            var settings = AppState.Load();
            if (settings.ViewConfig.ShowDayPreviewOnFocusStart)
            {
                try
                {
                    var preview = new Dialogs.DayPreviewDialog(settings, _clock.Now) { XamlRoot = this.XamlRoot };
                    await preview.ShowAsync();
                }
                catch { /* si no se puede mostrar, no bloquear el inicio */ }
            }

            ResolveContext();          // recoge el bloque vigente en el momento de arrancar
            _engine.Start(_clock.Now);
        }
        else if (!_engine.IsRunning) _engine.Resume(_clock.Now);
        _ticker?.Start();
        Refresh();
        if (wasIdle) EnterCompact();   // al entrar en concentración, minimiza a la isla (#118)
    }

    private void PauseBtn_Click(object sender, RoutedEventArgs e)
    {
        _engine.Pause(_clock.Now);
        _ticker?.Stop();
        Refresh();
    }

    private void SkipBtn_Click(object sender, RoutedEventArgs e)
    {
        _engine.Skip(_clock.Now);
        Refresh();
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _engine.Reset();
        _ticker?.Stop();
        if (_compact) ExitCompact();   // al parar, vuelve a la app y cierra la isla (#118)
        Refresh();
    }

    private void Refresh()
    {
        var now = _clock.Now;
        _engine.Advance(now);

        var remaining = _engine.Remaining(now);
        TimeText.Text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";

        var total = _engine.CurrentPhaseDuration.TotalSeconds;
        var prog = total <= 0 ? 0.0 : 1.0 - (remaining.TotalSeconds / total);
        if (double.IsNaN(prog) || double.IsInfinity(prog)) prog = 0.0;
        Progress.Value = Math.Clamp(prog, 0.0, 1.0);

        PhaseText.Text = _engine.Phase switch
        {
            PomodoroPhase.Focus => "🎯 Concentración",
            PomodoroPhase.ShortBreak => "☕ Descanso corto",
            PomodoroPhase.LongBreak => "🌴 Descanso largo",
            _ => "Listo para empezar"
        };
        FocusCount.Text = $"Focos completados: {_engine.CompletedFocuses}";

        var idle = _engine.Phase == PomodoroPhase.Idle;
        var running = _engine.IsRunning;
        StartBtn.IsEnabled = idle || !running;
        StartBtnText.Text = idle ? "Iniciar" : "Reanudar";
        PauseBtn.IsEnabled = running;
        SkipBtn.IsEnabled = !idle;
        ResetBtn.IsEnabled = !idle;

        SyncFocusMode();

        // Isla flotante (#118): la hora del sistema manda + el cronómetro Pomodoro.
        if (_compact && _overlay is not null)
        {
            var es = new System.Globalization.CultureInfo("es-ES");
            string phaseLabel = _engine.Phase switch
            {
                PomodoroPhase.Focus => "CONCENTRACIÓN",
                PomodoroPhase.ShortBreak => "DESCANSO CORTO",
                PomodoroPhase.LongBreak => "DESCANSO LARGO",
                _ => "EN PAUSA"
            };
            _overlay.UpdateView(
                clock: now.ToString("HH\\:mm"),
                date: Capitalize(now.ToString("dddd d 'de' MMMM", es)),
                phaseLabel: phaseLabel,
                pomo: $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}",
                progress: Math.Clamp(prog, 0.0, 1.0),
                isRunning: running,
                canSkip: !idle);
        }
    }

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    // ---------- Isla flotante de concentración (#118) ----------

    /// <summary>Minimiza la app y muestra la isla flotante arriba a la derecha.</summary>
    private void EnterCompact()
    {
        if (_overlay is null)
        {
            _overlay = new FocusOverlayWindow();
            _overlay.ExpandRequested += ExitCompact;
            _overlay.PauseResumeRequested += () =>
            {
                if (_engine.IsRunning) PauseBtn_Click(this, new RoutedEventArgs());
                else StartBtn_Click(this, new RoutedEventArgs());   // reanuda
            };
            _overlay.SkipRequested += () => SkipBtn_Click(this, new RoutedEventArgs());
            _overlay.Closed += (_, _) => { _overlay = null; _compact = false; };
        }
        _compact = true;
        _overlay.Activate();
        (MainWindow.Current?.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter)?.Minimize();
        Refresh();   // pinta la isla de inmediato
    }

    /// <summary>Cierra la isla y restaura la app a tamaño normal.</summary>
    private void ExitCompact()
    {
        _compact = false;
        if (_overlay is not null) { var o = _overlay; _overlay = null; o.Close(); }
        var main = MainWindow.Current;
        if (main is not null)
        {
            (main.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter)?.Restore();
            main.Activate();
        }
    }

    private void CompactBtn_Click(object sender, RoutedEventArgs e) => EnterCompact();

    /// <summary>
    /// Mantiene el "No molestar" del SO sincronizado: activo SOLO mientras hay
    /// una fase de Concentración en marcha; restaurado en descansos, pausa o parada.
    /// </summary>
    private void SyncFocusMode()
    {
        bool shouldFocus = _engine.Phase == PomodoroPhase.Focus && _engine.IsRunning;
        if (shouldFocus && !_focus.IsActive)
        {
            _focus.Enter();
            // Acciones del entorno mapeado al bloque actual, una sola vez por sesión (#67).
            if (!_environmentApplied)
            {
                _environmentApplied = true;
                var env = _activeEnv;
                if (env is not null)
                {
                    // Solo el subconjunto de apps/enlaces de la categoría de sesión activa (#116);
                    // sin perfil para ese título, ResolveOpen devuelve todo (por defecto).
                    var (openLinks, openApps) = env.ResolveOpen(_activeSessionTitle);
                    void LaunchWorkspace()
                    {
                        AppLauncher.OpenAll(openApps);           // abrir herramientas de trabajo (#109)
                        var urls = openLinks.Select(l => l.Url).ToList();
                        // Música WEB (Navidrome / URL http): si el entorno abre ventana nueva, debe abrirse
                        // EN esa misma ventana junto a los enlaces, no en el navegador principal. Si NO se
                        // abre ventana nueva, se lanza por su cuenta en el navegador por defecto. (#146)
                        bool musicInNewWindow = env.OpenLinksInBrowser && IsWebMusic(env.Music);
                        if (musicInNewWindow) urls.Add(env.Music!.Target.Trim());
                        if (env.OpenLinksInBrowser && urls.Count > 0)
                            DefaultBrowser.OpenLinksInNewWindow(urls);
                        if (!musicInNewWindow) MusicService.TryLaunch(env.Music);   // app/URI o sin ventana nueva
                    }

                    if (env.NewVirtualDesktop)                   // escritorio virtual limpio PRIMERO (#110)
                    {
                        VirtualDesktops.CreateAndSwitch();
                        _createdDesktop = true;
                    }
                    AppCloser.CloseAll(env.AppsToClose);         // cerrar apps de ruido (#35)
                    AppMuter.Mute(env.AppsToMute);               // silenciar apps de ruido (#9)
                    if (env.HideTaskbarBadges && MainWindow.Current is not null)   // sin parpadeos/badge (#31)
                        TaskbarSilencer.Suppress(WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current));

                    if (env.NewVirtualDesktop)
                        // El cambio de escritorio (SendInput Win+Ctrl+D) es ASÍNCRONO a nivel del SO:
                        // si lanzáramos las apps/navegador ya, se abrirían en el escritorio viejo (#141).
                        // Esperamos a que el escritorio nuevo esté activo (fuera de la UI, best-effort).
                        _ = System.Threading.Tasks.Task.Delay(WorkspaceLaunchDelayMs).ContinueWith(_ => LaunchWorkspace());
                    else
                        LaunchWorkspace();                       // sin escritorio nuevo: abrir ya, en el actual
                }
            }
        }
        else if (!shouldFocus && _focus.IsActive)
        {
            _focus.Exit();   // en descansos solo se restaura No molestar; el escritorio se mantiene
        }

        if (_engine.Phase == PomodoroPhase.Idle)
        {
            _environmentApplied = false;   // reset al parar
            AppMuter.RestoreAll();         // restaurar el audio de las apps silenciadas (#9)
            TaskbarSilencer.Restore();     // volver a permitir parpadeos/badge (#31)
            if (_createdDesktop) { VirtualDesktops.CloseCurrent(); _createdDesktop = false; }   // cerrar el escritorio de concentración
        }
        // Bloqueo blando de webs distractoras mientras dura la concentración (#8/#33): minimiza
        // las ventanas de navegador cuya pestaña activa esté en una web bloqueada del entorno.
        bool blocking = _focus.IsActive && _activeEnv is { BlockedWebsites.Count: > 0 };
        if (blocking) DistractionGuard.Sweep(_activeEnv!.BlockedWebsites);
        // Bloqueo duro (#8): publica el estado para la extensión de navegador.
        BlockStateServer.SetState(blocking, _activeEnv?.BlockedWebsites ?? Array.Empty<string>());

        DndBadge.Visibility = _focus.IsActive ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// ¿La música del entorno es WEB (se abre en navegador)? Navidrome (proveedor) o cualquier
    /// Target http(s). Las apps/URIs (p. ej. "spotify:") NO lo son. (#146)
    /// </summary>
    private static bool IsWebMusic(Ritmo.Core.Focus.MusicLauncher? m)
        => m is not null && !string.IsNullOrWhiteSpace(m.Target) && (
               string.Equals(m.Provider, "navidrome", StringComparison.OrdinalIgnoreCase)
               || m.Target.TrimStart().StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || m.Target.TrimStart().StartsWith("https://", StringComparison.OrdinalIgnoreCase));
}
