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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ResolveContext();

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
            _activeEnv = settings.ResolveEnvironment(StudyKind.Otro);   // cae al entorno por defecto
            config = settings.Pomodoro;
            SubjectText.Text = "Sin bloque ahora";
            SubjectMeta.Text = "Concentración libre";
        }
        SubjectBadge.Visibility = Visibility.Visible;

        if (_engine.Phase == PomodoroPhase.Idle)
            _engine = new PomodoroEngine(config);

        PresetInfo.Text =
            $"Concentración {config.Focus.TotalMinutes:0} · descanso {config.ShortBreak.TotalMinutes:0} · " +
            $"largo {config.LongBreak.TotalMinutes:0} cada {config.FocusesPerLongBreak} focos";
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
                    MusicService.TryLaunch(env.Music);          // lanzar música (#10)
                    AppCloser.CloseAll(env.AppsToClose);         // cerrar apps de ruido (#35)
                    if (env.OpenStudyListInEdge)
                        EdgeStudyProfile.OpenStudyProfile();     // abrir perfil de estudio en Edge (#11)
                }
            }
        }
        else if (!shouldFocus && _focus.IsActive)
        {
            _focus.Exit();
        }

        if (_engine.Phase == PomodoroPhase.Idle) _environmentApplied = false;  // reset al parar
        DndBadge.Visibility = _focus.IsActive ? Visibility.Visible : Visibility.Collapsed;
    }
}
