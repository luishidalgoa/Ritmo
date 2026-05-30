using System;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
/// del plan, muestra su asignatura, usa el preset Pomodoro del entorno mapeado a
/// su tipo y, al concentrar, aplica ese entorno (no el genérico por defecto).
/// </summary>
public sealed partial class TimerPage : Page
{
    private readonly IClock _clock = new SystemClock();
    private PomodoroEngine _engine = new(PomodoroConfig.DeepWork);
    private readonly IFocusController _focus = new WindowsFocusController();
    private DispatcherQueueTimer? _ticker;
    private bool _environmentApplied;
    private bool _createdDesktop;   // #110: creamos un escritorio virtual esta sesión

    // Contexto del bloque vigente (resuelto desde el horario).
    private FocusEnvironment? _activeEnv;

    /// <summary>
    /// Si otra pantalla (Hoy / Horario) pide "empezar ya", lo deja marcado aquí;
    /// el Timer lo consume al cargarse y arranca la concentración automáticamente.
    /// </summary>
    public static bool AutoStartPending { get; set; }

    public TimerPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => { _ticker?.Stop(); _focus.Exit(); };
    }

    private bool _loadingEnv;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ResolveContext();
        BuildEnvSelector(AppState.Load());

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

    /// <summary>
    /// Mira el horario: si hay un bloque activo AHORA, carga su asignatura, el
    /// entorno mapeado a su tipo y el preset Pomodoro de ese entorno. Si no hay
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
        var active = new SchedulePlanner(schedule).GetActiveSession(now);

        PomodoroConfig config;
        if (active is not null)
        {
            _activeEnv = settings.ResolveEnvironment(active.Kind);
            config = PomodoroRhythms.Resolve(_activeEnv?.PomodoroPreset, settings.Rhythms, settings.Pomodoro);
            SubjectText.Text = active.Title;
            SubjectMeta.Text = $"{active.Kind.Label()} · {active.Start:HH\\:mm}–{active.End:HH\\:mm}";
        }
        else
        {
            _activeEnv = settings.ResolveEnvironment(StudyKind.Otro);   // el entorno seleccionado/por defecto
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

    private void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.Phase == PomodoroPhase.Idle)
        {
            ResolveContext();          // recoge el bloque vigente en el momento de arrancar
            _engine.Start(_clock.Now);
        }
        else if (!_engine.IsRunning) _engine.Resume(_clock.Now);
        _ticker?.Start();
        Refresh();
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
    }

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
                    if (env.NewVirtualDesktop)                   // escritorio virtual limpio PRIMERO (#110)
                    {
                        VirtualDesktops.CreateAndSwitch();
                        _createdDesktop = true;
                    }
                    MusicService.TryLaunch(env.Music);          // lanzar música (#10)
                    AppCloser.CloseAll(env.AppsToClose);         // cerrar apps de ruido (#35)
                    AppLauncher.OpenAll(env.AppsToOpen);         // abrir herramientas de estudio (#109)
                    if (env.OpenLinksInBrowser && env.Links.Count > 0)   // abrir enlaces en ventana nueva del navegador por defecto
                        DefaultBrowser.OpenLinksInNewWindow(env.Links.Select(l => l.Url).ToList());
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
            if (_createdDesktop) { VirtualDesktops.CloseCurrent(); _createdDesktop = false; }   // cerrar el escritorio de estudio
        }
        DndBadge.Visibility = _focus.IsActive ? Visibility.Visible : Visibility.Collapsed;
    }
}
