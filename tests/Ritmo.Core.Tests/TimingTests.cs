using Ritmo.Core.Timing;

namespace Ritmo.Core.Tests;

public class ManualClockTests
{
    private static readonly DateTime T0 = new(2026, 6, 1, 9, 0, 0);

    [Fact]
    public void Advance_suma_el_tiempo()
    {
        var c = new ManualClock(T0);
        c.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal(T0.AddMinutes(30), c.Now);
    }

    [Fact]
    public void Set_fija_instante_absoluto()
    {
        var c = new ManualClock(T0);
        c.Set(T0.AddHours(2));
        Assert.Equal(T0.AddHours(2), c.Now);
    }

    [Fact]
    public void No_permite_retroceder()
    {
        var c = new ManualClock(T0);
        Assert.Throws<ArgumentOutOfRangeException>(() => c.Set(T0.AddMinutes(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => c.Advance(TimeSpan.FromMinutes(-1)));
    }
}

public class ManualSchedulerTests
{
    private static readonly DateTime T0 = new(2026, 6, 1, 9, 0, 0);

    private static (ManualClock, ManualScheduler) New()
    {
        var clock = new ManualClock(T0);
        return (clock, new ManualScheduler(clock));
    }

    [Fact]
    public void No_dispara_antes_de_vencer()
    {
        var (_, s) = New();
        var fired = false;
        s.Schedule(TimeSpan.FromMinutes(10), () => fired = true);

        s.Advance(TimeSpan.FromMinutes(9));
        Assert.False(fired);
        Assert.Equal(1, s.PendingCount);
    }

    [Fact]
    public void Dispara_al_vencer()
    {
        var (_, s) = New();
        var fired = false;
        s.Schedule(TimeSpan.FromMinutes(10), () => fired = true);

        s.Advance(TimeSpan.FromMinutes(10));
        Assert.True(fired);
        Assert.Equal(0, s.PendingCount);
    }

    [Fact]
    public void Dispara_en_orden_cronologico()
    {
        var (_, s) = New();
        var order = new List<string>();
        s.Schedule(TimeSpan.FromMinutes(20), () => order.Add("b"));
        s.Schedule(TimeSpan.FromMinutes(10), () => order.Add("a"));
        s.Schedule(TimeSpan.FromMinutes(30), () => order.Add("c"));

        s.Advance(TimeSpan.FromHours(1));
        Assert.Equal(new[] { "a", "b", "c" }, order);
    }

    [Fact]
    public void Dispose_cancela_antes_de_disparar()
    {
        var (_, s) = New();
        var fired = false;
        var handle = s.Schedule(TimeSpan.FromMinutes(10), () => fired = true);

        handle.Dispose();
        s.Advance(TimeSpan.FromMinutes(20));
        Assert.False(fired);
        Assert.Equal(0, s.PendingCount);
    }

    [Fact]
    public void Un_callback_que_reprograma_dentro_del_rango_se_dispara_en_la_misma_pasada()
    {
        var (_, s) = New();
        var hits = new List<int>();
        // El primer callback vence a los 10m y, en ese momento (reloj=+10m),
        // programa otro a +5m (vence en +15m). Avanzamos a +20m: ambos caen dentro.
        s.Schedule(TimeSpan.FromMinutes(10), () =>
        {
            hits.Add(1);
            s.Schedule(TimeSpan.FromMinutes(5), () => hits.Add(2));
        });

        s.AdvanceTo(T0.AddMinutes(20));
        // El primero dispara con reloj=+10m y reprograma a +15m, que <= +20m -> también dispara.
        Assert.Equal(new[] { 1, 2 }, hits);
        Assert.Equal(0, s.PendingCount);
    }

    [Fact]
    public void Reprogramacion_fuera_del_rango_queda_pendiente()
    {
        var (_, s) = New();
        var hits = new List<int>();
        s.Schedule(TimeSpan.FromMinutes(10), () =>
        {
            hits.Add(1);
            // Con reloj=+10m, +5m vence en +15m: fuera del AdvanceTo(+12m).
            s.Schedule(TimeSpan.FromMinutes(5), () => hits.Add(2));
        });

        s.AdvanceTo(T0.AddMinutes(12));
        Assert.Equal(new[] { 1 }, hits);     // solo el primero
        Assert.Equal(1, s.PendingCount);     // el reprogramado queda esperando
    }

    [Fact]
    public void Delay_no_positivo_dispara_en_el_proximo_advance()
    {
        var (_, s) = New();
        var fired = false;
        s.Schedule(TimeSpan.Zero, () => fired = true);
        // Aún no ha habido advance -> no se ha llamado a FireDue.
        Assert.False(fired);
        s.Advance(TimeSpan.Zero); // dispara lo vencido (due <= now)
        Assert.True(fired);
    }
}

public class SystemSchedulerTests
{
    [Fact]
    public async Task SystemScheduler_dispara_tras_el_delay_real()
    {
        var sched = new SystemScheduler();
        var tcs = new TaskCompletionSource();
        sched.Schedule(TimeSpan.FromMilliseconds(40), () => tcs.TrySetResult());

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Equal(tcs.Task, completed); // se disparó dentro del margen
    }

    [Fact]
    public async Task SystemScheduler_Dispose_evita_el_disparo()
    {
        var sched = new SystemScheduler();
        var fired = 0;
        var handle = sched.Schedule(TimeSpan.FromMilliseconds(80), () => Interlocked.Increment(ref fired));
        handle.Dispose();

        await Task.Delay(250);
        Assert.Equal(0, Volatile.Read(ref fired));
    }
}
