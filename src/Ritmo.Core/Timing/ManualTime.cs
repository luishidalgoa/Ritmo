namespace Ritmo.Core.Timing;

/// <summary>
/// Reloj controlable manualmente. Pensado para tests: el tiempo solo avanza
/// cuando se llama a Advance/Set. También sirve de motor para ManualScheduler.
/// </summary>
public sealed class ManualClock : IClock
{
    public DateTime Now { get; private set; }

    public ManualClock(DateTime start) => Now = start;

    /// <summary>Fija la hora a un instante absoluto (debe ir hacia delante).</summary>
    public void Set(DateTime now)
    {
        if (now < Now)
            throw new ArgumentOutOfRangeException(nameof(now), "El tiempo no puede retroceder.");
        Now = now;
    }

    /// <summary>Avanza el reloj la cantidad indicada.</summary>
    public void Advance(TimeSpan by)
    {
        if (by < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(by), "No se puede avanzar un tiempo negativo.");
        Now += by;
    }
}

/// <summary>
/// Scheduler determinista para tests. No usa timers reales: las acciones se
/// disparan cuando el reloj asociado alcanza su instante de vencimiento, lo cual
/// ocurre al llamar a Advance/Set en este scheduler (que mueve su ManualClock).
/// Así el servicio en segundo plano se prueba sin esperar tiempo real.
/// </summary>
public sealed class ManualScheduler : IScheduler
{
    private readonly ManualClock _clock;
    private readonly List<Entry> _entries = [];

    public ManualScheduler(ManualClock clock) => _clock = clock;

    /// <summary>Número de callbacks pendientes (aún no disparados ni cancelados).</summary>
    public int PendingCount => _entries.Count;

    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

        var entry = new Entry(this, _clock.Now + delay, callback);
        _entries.Add(entry);
        return entry;
    }

    /// <summary>
    /// Avanza el reloj asociado hasta 'ahora + by', disparando en orden
    /// cronológico cada callback vencido. El reloj se mueve HASTA el instante de
    /// cada vencimiento antes de ejecutarlo, de modo que un callback que
    /// reprograma lo hace relativo a SU instante real de disparo (no al destino
    /// final). Así el tiempo es predecible y realista.
    /// </summary>
    public void Advance(TimeSpan by)
    {
        if (by < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(by), "No se puede avanzar un tiempo negativo.");
        AdvanceTo(_clock.Now + by);
    }

    /// <summary>Como Advance pero a un instante absoluto.</summary>
    public void AdvanceTo(DateTime target)
    {
        if (target < _clock.Now)
            throw new ArgumentOutOfRangeException(nameof(target), "El tiempo no puede retroceder.");

        // Procesa vencimientos uno a uno, moviendo el reloj hasta cada uno.
        while (true)
        {
            var due = _entries
                .Where(e => !e.Cancelled && e.DueAt <= target)
                .OrderBy(e => e.DueAt)
                .FirstOrDefault();
            if (due is null) break;

            _entries.Remove(due);
            // Mover el reloj al instante exacto del vencimiento antes de disparar.
            if (due.DueAt > _clock.Now)
                _clock.Set(due.DueAt);
            if (!due.Cancelled)
                due.Callback(); // si reprograma, lo hace desde _clock.Now == due.DueAt
        }

        // Llevar el reloj al destino final aunque no quedaran más vencimientos.
        if (target > _clock.Now)
            _clock.Set(target);
    }

    private sealed class Entry(ManualScheduler owner, DateTime dueAt, Action callback) : IDisposable
    {
        public DateTime DueAt { get; } = dueAt;
        public Action Callback { get; } = callback;
        public bool Cancelled { get; private set; }

        public void Dispose()
        {
            Cancelled = true;
            owner._entries.Remove(this);
        }
    }
}
