namespace Ritmo.Core.Timing;

/// <summary>
/// Scheduler real basado en System.Threading.Timer. Úsese en producción.
/// Cada Schedule crea un timer de un solo disparo; al dispararse o al desecharlo,
/// el timer se libera.
/// </summary>
public sealed class SystemScheduler : IScheduler
{
    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

        // Timer de un solo disparo (period = Infinite).
        var handle = new TimerHandle();
        handle.Timer = new Timer(_ =>
        {
            // Evitar doble disparo si justo coincide con un Dispose.
            if (handle.TryFire())
                callback();
        }, null, delay, Timeout.InfiniteTimeSpan);
        return handle;
    }

    private sealed class TimerHandle : IDisposable
    {
        private int _fired;
        public Timer? Timer;

        public bool TryFire()
        {
            // Solo el primero en llegar dispara; luego libera el timer.
            if (Interlocked.Exchange(ref _fired, 1) == 0)
            {
                Timer?.Dispose();
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            // Marcar como ya disparado para que un callback en vuelo no ejecute.
            Interlocked.Exchange(ref _fired, 1);
            Timer?.Dispose();
        }
    }
}
