using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Pomodoro;
using Ritmo.Core.Timing;

namespace Ritmo_App;

/// <summary>
/// Vista del temporizador Pomodoro: refleja en pantalla el PomodoroEngine del
/// núcleo. Un DispatcherQueueTimer refresca la UI; el motor recibe el "ahora"
/// del SystemClock (la lógica vive en el núcleo, ya testeada).
/// </summary>
public sealed partial class TimerPage : Page
{
    private readonly IClock _clock = new SystemClock();
    private readonly PomodoroEngine _engine = new(PomodoroConfig.DeepWork);
    private DispatcherQueueTimer? _ticker;

    public TimerPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _ticker?.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var c = PomodoroConfig.DeepWork;
        PresetInfo.Text =
            $"Concentración {c.Focus.TotalMinutes:0} · descanso {c.ShortBreak.TotalMinutes:0} · " +
            $"largo {c.LongBreak.TotalMinutes:0} cada {c.FocusesPerLongBreak} focos";

        _ticker = DispatcherQueue.CreateTimer();
        _ticker.Interval = TimeSpan.FromMilliseconds(250);
        _ticker.Tick += (_, _) => Refresh();

        Refresh();
    }

    private void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.Phase == PomodoroPhase.Idle) _engine.Start(_clock.Now);
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
    }
}
