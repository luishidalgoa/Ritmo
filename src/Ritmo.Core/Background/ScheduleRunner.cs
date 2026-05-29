using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;
using Ritmo.Core.Timing;

namespace Ritmo.Core.Background;

/// <summary>
/// Servicio (cerebro) que vive en segundo plano: pregunta al planificador cuál es
/// el próximo evento del horario semanal, programa un timer para ese instante con
/// el <see cref="IScheduler"/>, y al dispararse lo notifica y reprograma el siguiente.
///
/// No conoce la UI ni el SO: solo orquesta. Recibe reloj y scheduler por inyección,
/// así se testea de forma determinista con ManualClock/ManualScheduler.
/// </summary>
public sealed class ScheduleRunner : IDisposable
{
    private readonly SchedulePlanner _planner;
    private readonly IClock _clock;
    private readonly IScheduler _scheduler;

    private IDisposable? _pending;
    private bool _running;
    private bool _disposed;

    /// <summary>Se dispara cuando llega el momento de un evento (aviso o inicio de sesión).</summary>
    public event Action<PlannedEvent>? EventDue;

    /// <summary>El evento que está actualmente programado y esperando (o null).</summary>
    public PlannedEvent? NextScheduled { get; private set; }

    public ScheduleRunner(WeeklySchedule schedule, IClock clock, IScheduler scheduler)
        : this(new SchedulePlanner(schedule), clock, scheduler) { }

    public ScheduleRunner(SchedulePlanner planner, IClock clock, IScheduler scheduler)
    {
        _planner = planner;
        _clock = clock;
        _scheduler = scheduler;
    }

    /// <summary>Arranca el servicio: programa el primer evento futuro.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running) return;
        _running = true;
        ScheduleNext();
    }

    /// <summary>Detiene el servicio y cancela el timer pendiente.</summary>
    public void Stop()
    {
        _running = false;
        _pending?.Dispose();
        _pending = null;
        NextScheduled = null;
    }

    private void ScheduleNext()
    {
        _pending?.Dispose();
        _pending = null;
        NextScheduled = null;
        if (!_running) return;

        var now = _clock.Now;
        var next = _planner.GetNextEvent(now);
        if (next is null) return; // nada en el horizonte; el host puede re-armar más tarde

        NextScheduled = next;
        var delay = next.At - now;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

        _pending = _scheduler.Schedule(delay, () => Fire(next));
    }

    private void Fire(PlannedEvent ev)
    {
        if (!_running) return;
        EventDue?.Invoke(ev);
        // Encadena el siguiente evento (estrictamente posterior, lo garantiza GetNextEvent).
        ScheduleNext();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
