namespace Ritmo.Core.Pomodoro;

/// <summary>
/// Resultado de avanzar el reloj: indica si una fase se completó al hacer tick.
/// </summary>
public readonly record struct TickResult(bool PhaseCompleted, PomodoroPhase CompletedPhase, PomodoroPhase NewPhase);

/// <summary>
/// Motor del ciclo Pomodoro. Lleva la fase, el tiempo transcurrido y los focos
/// completados. Es DETERMINISTA: nunca lee el reloj por dentro — el host le pasa
/// el instante actual en Start/Advance. Así se testea con tiempo simulado.
///
/// Ciclo: Focus -> ShortBreak -> Focus -> ... -> (cada N focos) LongBreak -> Focus...
/// </summary>
public sealed class PomodoroEngine
{
    private readonly PomodoroConfig _config;

    // Instante en el que arrancó la fase actual (si está corriendo).
    private DateTime _phaseStartedAt;
    // Tiempo ya consumido de la fase actual antes de la última reanudación (para pausas).
    private TimeSpan _elapsedBeforeRunning;
    private bool _running;

    public PomodoroEngine(PomodoroConfig config) => _config = config;

    /// <summary>Fase actual.</summary>
    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Idle;

    /// <summary>¿Está el temporizador corriendo (no pausado, no idle)?</summary>
    public bool IsRunning => _running;

    /// <summary>Número de focos completados en este ciclo (para el descanso largo).</summary>
    public int CompletedFocuses { get; private set; }

    /// <summary>Duración total de la fase actual según la config.</summary>
    public TimeSpan CurrentPhaseDuration => DurationOf(Phase);

    /// <summary>Arranca una nueva sesión desde Idle, comenzando en Focus.</summary>
    public void Start(DateTime now)
    {
        Phase = PomodoroPhase.Focus;
        CompletedFocuses = 0;
        _elapsedBeforeRunning = TimeSpan.Zero;
        _phaseStartedAt = now;
        _running = true;
    }

    /// <summary>Tiempo transcurrido de la fase actual en el instante 'now'.</summary>
    public TimeSpan Elapsed(DateTime now)
    {
        if (Phase == PomodoroPhase.Idle) return TimeSpan.Zero;
        var running = _running ? (now - _phaseStartedAt) : TimeSpan.Zero;
        var total = _elapsedBeforeRunning + running;
        return total < TimeSpan.Zero ? TimeSpan.Zero : total;
    }

    /// <summary>Tiempo que falta para terminar la fase actual.</summary>
    public TimeSpan Remaining(DateTime now)
    {
        if (Phase == PomodoroPhase.Idle) return TimeSpan.Zero;
        var rem = DurationOf(Phase) - Elapsed(now);
        return rem < TimeSpan.Zero ? TimeSpan.Zero : rem;
    }

    /// <summary>
    /// Avanza el reloj hasta 'now'. Si la fase actual ya se ha completado,
    /// transiciona a la siguiente y devuelve el resultado. Solo procesa una
    /// transición por llamada (el host hace tick con frecuencia).
    /// </summary>
    public TickResult Advance(DateTime now)
    {
        if (Phase == PomodoroPhase.Idle || !_running)
            return new TickResult(false, Phase, Phase);

        if (Elapsed(now) < DurationOf(Phase))
            return new TickResult(false, Phase, Phase);

        // Fase completada -> transición.
        var completed = Phase;
        if (completed == PomodoroPhase.Focus)
            CompletedFocuses++;

        var next = NextPhase(completed);
        Phase = next;
        _elapsedBeforeRunning = TimeSpan.Zero;
        _phaseStartedAt = now;
        _running = true;
        return new TickResult(true, completed, next);
    }

    /// <summary>Pausa el temporizador, congelando el tiempo transcurrido.</summary>
    public void Pause(DateTime now)
    {
        if (!_running || Phase == PomodoroPhase.Idle) return;
        _elapsedBeforeRunning += (now - _phaseStartedAt);
        _running = false;
    }

    /// <summary>Reanuda tras una pausa.</summary>
    public void Resume(DateTime now)
    {
        if (_running || Phase == PomodoroPhase.Idle) return;
        _phaseStartedAt = now;
        _running = true;
    }

    /// <summary>Salta a la siguiente fase inmediatamente (cuenta el foco si se salta un Focus).</summary>
    public TickResult Skip(DateTime now)
    {
        if (Phase == PomodoroPhase.Idle)
            return new TickResult(false, Phase, Phase);

        var completed = Phase;
        if (completed == PomodoroPhase.Focus)
            CompletedFocuses++;

        var next = NextPhase(completed);
        Phase = next;
        _elapsedBeforeRunning = TimeSpan.Zero;
        _phaseStartedAt = now;
        _running = true;
        return new TickResult(true, completed, next);
    }

    /// <summary>Detiene y vuelve a Idle, reseteando el contador de focos.</summary>
    public void Reset()
    {
        Phase = PomodoroPhase.Idle;
        CompletedFocuses = 0;
        _elapsedBeforeRunning = TimeSpan.Zero;
        _running = false;
    }

    private TimeSpan DurationOf(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.Focus => _config.Focus,
        PomodoroPhase.ShortBreak => _config.ShortBreak,
        PomodoroPhase.LongBreak => _config.LongBreak,
        _ => TimeSpan.Zero
    };

    private PomodoroPhase NextPhase(PomodoroPhase completed) => completed switch
    {
        // Tras un foco: descanso largo si toca por el contador, si no, corto.
        PomodoroPhase.Focus =>
            (CompletedFocuses % _config.FocusesPerLongBreak == 0)
                ? PomodoroPhase.LongBreak
                : PomodoroPhase.ShortBreak,
        // Tras cualquier descanso: volver a concentrarse.
        PomodoroPhase.ShortBreak => PomodoroPhase.Focus,
        PomodoroPhase.LongBreak => PomodoroPhase.Focus,
        _ => PomodoroPhase.Idle
    };
}
