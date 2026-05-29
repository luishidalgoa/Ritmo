using Ritmo.Core.Background;
using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;
using Ritmo.Core.Timing;

namespace Ritmo.Core.Tests;

public class ScheduleRunnerTests
{
    // Lunes 09:00 Técnico (2h) con avisos 60 y 10 min antes.
    private static WeeklySchedule Schedule() => new()
    {
        Sessions =
        [
            new StudySession
            {
                Title = "Técnico",
                Day = DayOfWeek.Monday,
                Start = new TimeOnly(9, 0),
                Duration = TimeSpan.FromHours(2),
                Kind = StudyKind.Tecnico,
                PreAlerts = [PreAlert.OneHour, PreAlert.TenMinutes]
            }
        ]
    };

    // Lunes 1 jun 2026, 00:00.
    private static readonly DateTime MondayMidnight = new(2026, 6, 1, 0, 0, 0);

    private static (ScheduleRunner, ManualScheduler, List<PlannedEvent>) Build(DateTime start)
    {
        var clock = new ManualClock(start);
        var scheduler = new ManualScheduler(clock);
        var runner = new ScheduleRunner(Schedule(), clock, scheduler);
        var fired = new List<PlannedEvent>();
        runner.EventDue += e => fired.Add(e);
        return (runner, scheduler, fired);
    }

    [Fact]
    public void Al_arrancar_programa_el_primer_evento()
    {
        var (runner, _, _) = Build(MondayMidnight);
        runner.Start();
        Assert.NotNull(runner.NextScheduled);
        // El primero del lunes es el aviso de 60 min -> 08:00.
        Assert.Equal(new DateTime(2026, 6, 1, 8, 0, 0), runner.NextScheduled!.At);
        Assert.Equal(PlannedEventType.PreAlert, runner.NextScheduled.Type);
    }

    [Fact]
    public void Dispara_avisos_e_inicio_en_orden_y_momento_correctos()
    {
        var (runner, scheduler, fired) = Build(MondayMidnight);
        runner.Start();

        // Avanzar todo el lunes.
        scheduler.AdvanceTo(new DateTime(2026, 6, 1, 23, 59, 0));

        Assert.Equal(3, fired.Count);
        // Aviso 60' -> 08:00
        Assert.Equal(PlannedEventType.PreAlert, fired[0].Type);
        Assert.Equal(60, fired[0].MinutesBefore);
        Assert.Equal(new DateTime(2026, 6, 1, 8, 0, 0), fired[0].At);
        // Aviso 10' -> 08:50
        Assert.Equal(10, fired[1].MinutesBefore);
        Assert.Equal(new DateTime(2026, 6, 1, 8, 50, 0), fired[1].At);
        // Inicio -> 09:00
        Assert.Equal(PlannedEventType.SessionStart, fired[2].Type);
        Assert.Equal(new DateTime(2026, 6, 1, 9, 0, 0), fired[2].At);
    }

    [Fact]
    public void No_dispara_nada_antes_del_primer_evento()
    {
        var (runner, scheduler, fired) = Build(MondayMidnight);
        runner.Start();
        scheduler.AdvanceTo(new DateTime(2026, 6, 1, 7, 59, 0)); // justo antes del aviso de las 08:00
        Assert.Empty(fired);
    }

    [Fact]
    public void Stop_cancela_el_evento_pendiente()
    {
        var (runner, scheduler, fired) = Build(MondayMidnight);
        runner.Start();
        runner.Stop();
        Assert.Null(runner.NextScheduled);
        scheduler.AdvanceTo(new DateTime(2026, 6, 1, 12, 0, 0));
        Assert.Empty(fired);
    }

    [Fact]
    public void Tras_disparar_reprograma_el_siguiente()
    {
        var (runner, scheduler, fired) = Build(MondayMidnight);
        runner.Start();
        // Avanzar solo hasta pasado el primer aviso (08:00).
        scheduler.AdvanceTo(new DateTime(2026, 6, 1, 8, 30, 0));
        Assert.Single(fired);
        // Ya debe estar programado el siguiente (aviso de 08:50).
        Assert.NotNull(runner.NextScheduled);
        Assert.Equal(new DateTime(2026, 6, 1, 8, 50, 0), runner.NextScheduled!.At);
    }

    [Fact]
    public void Cruza_a_la_semana_siguiente_si_no_hay_eventos_esta()
    {
        // Empezamos un martes: el siguiente Técnico es el lunes que viene.
        var tuesday = new DateTime(2026, 6, 2, 0, 0, 0);
        var (runner, scheduler, fired) = Build(tuesday);
        runner.Start();
        Assert.NotNull(runner.NextScheduled);
        Assert.Equal(new DateTime(2026, 6, 8, 8, 0, 0), runner.NextScheduled!.At); // lunes siguiente, aviso 60'

        scheduler.AdvanceTo(new DateTime(2026, 6, 8, 9, 30, 0));
        Assert.Equal(3, fired.Count); // los 2 avisos + el inicio de la semana siguiente
    }

    [Fact]
    public void Dispose_detiene_el_servicio()
    {
        var (runner, scheduler, fired) = Build(MondayMidnight);
        runner.Start();
        runner.Dispose();
        scheduler.AdvanceTo(new DateTime(2026, 6, 1, 12, 0, 0));
        Assert.Empty(fired);
        Assert.Throws<ObjectDisposedException>(() => runner.Start());
    }
}
