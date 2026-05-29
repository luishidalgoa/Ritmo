using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

public class SchedulePhaseTests
{
    private static SchedulePhase Phase(string name, DateOnly from, DateOnly? to) => new()
    {
        Name = name,
        ValidFrom = from,
        ValidTo = to,
        Schedule = new WeeklySchedule()
    };

    [Fact]
    public void IsActiveOn_dentro_del_rango()
    {
        var p = Phase("F1", new DateOnly(2026, 6, 1), new DateOnly(2026, 10, 31));
        Assert.True(p.IsActiveOn(new DateOnly(2026, 8, 15)));
        Assert.True(p.IsActiveOn(new DateOnly(2026, 6, 1)));   // inicio inclusivo
        Assert.True(p.IsActiveOn(new DateOnly(2026, 10, 31))); // fin inclusivo
    }

    [Fact]
    public void IsActiveOn_fuera_del_rango()
    {
        var p = Phase("F1", new DateOnly(2026, 6, 1), new DateOnly(2026, 10, 31));
        Assert.False(p.IsActiveOn(new DateOnly(2026, 5, 31)));
        Assert.False(p.IsActiveOn(new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void ValidTo_nulo_es_indefinida()
    {
        var p = Phase("Indef", new DateOnly(2026, 6, 1), null);
        Assert.True(p.IsActiveOn(new DateOnly(2030, 1, 1)));
        Assert.False(p.IsActiveOn(new DateOnly(2026, 5, 1)));
    }
}

public class SchedulePlanTests
{
    private static SchedulePhase Phase(string name, DateOnly from, DateOnly? to, WeeklySchedule? sch = null) => new()
    {
        Name = name, ValidFrom = from, ValidTo = to, Schedule = sch ?? new WeeklySchedule()
    };

    // Las 3 fases reales del plan TAI.
    private static SchedulePlan TaiPlan() => new()
    {
        Phases =
        [
            Phase("Fase 1", new DateOnly(2026, 6, 1), new DateOnly(2026, 10, 31)),
            Phase("Fase 2", new DateOnly(2026, 11, 1), new DateOnly(2027, 2, 28)),
            Phase("Fase 3", new DateOnly(2027, 3, 1), new DateOnly(2027, 5, 31))
        ]
    };

    [Fact]
    public void GetActivePhase_devuelve_la_fase_vigente()
    {
        var plan = TaiPlan();
        Assert.Equal("Fase 1", plan.GetActivePhase(new DateOnly(2026, 8, 1))!.Name);
        Assert.Equal("Fase 2", plan.GetActivePhase(new DateOnly(2026, 12, 25))!.Name);
        Assert.Equal("Fase 3", plan.GetActivePhase(new DateOnly(2027, 5, 1))!.Name);
    }

    [Fact]
    public void GetActivePhase_null_si_ninguna_cubre()
    {
        var plan = TaiPlan();
        Assert.Null(plan.GetActivePhase(new DateOnly(2026, 1, 1)));  // antes de todo
        Assert.Null(plan.GetActivePhase(new DateOnly(2027, 7, 1)));  // después de todo
    }

    [Fact]
    public void GetNextPhase_la_que_empieza_despues()
    {
        var plan = TaiPlan();
        // Estando en Fase 1, la siguiente es Fase 2.
        var next = plan.GetNextPhase(new DateOnly(2026, 8, 1));
        Assert.Equal("Fase 2", next!.Name);
        Assert.Equal(new DateOnly(2026, 11, 1), next.ValidFrom);
    }

    [Fact]
    public void GetNextPhase_null_si_no_hay_futuras()
    {
        var plan = TaiPlan();
        Assert.Null(plan.GetNextPhase(new DateOnly(2027, 4, 1))); // ya en la última
    }

    [Fact]
    public void GetActiveSchedule_devuelve_el_de_la_fase_activa()
    {
        var sch = new WeeklySchedule
        {
            Sessions = [ new StudySession {
                Title = "T", Day = DayOfWeek.Monday,
                Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(2) } ]
        };
        var plan = new SchedulePlan { Phases = [ Phase("F", new DateOnly(2026,6,1), null, sch) ] };
        Assert.Single(plan.GetActiveSchedule(new DateOnly(2026, 7, 1)).Sessions);
        // Fuera de vigencia -> horario vacío.
        Assert.Empty(plan.GetActiveSchedule(new DateOnly(2026, 5, 1)).Sessions);
    }

    [Fact]
    public void Fases_consecutivas_no_solapan()
    {
        var plan = TaiPlan();
        Assert.Empty(plan.FindOverlaps());
    }

    [Fact]
    public void FindOverlaps_detecta_solape()
    {
        var plan = new SchedulePlan
        {
            Phases =
            [
                Phase("A", new DateOnly(2026, 6, 1), new DateOnly(2026, 10, 31)),
                Phase("B", new DateOnly(2026, 10, 15), new DateOnly(2026, 12, 31)) // pisa a A
            ]
        };
        var ov = plan.FindOverlaps();
        Assert.Single(ov);
    }

    [Fact]
    public void Si_dos_solapan_gana_la_de_inicio_mas_reciente()
    {
        var general = new WeeklySchedule { Sessions = [ new StudySession {
            Title = "general", Day = DayOfWeek.Monday, Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(1) } ] };
        var especial = new WeeklySchedule { Sessions = [ new StudySession {
            Title = "especial", Day = DayOfWeek.Monday, Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(1) } ] };

        var plan = new SchedulePlan
        {
            Phases =
            [
                Phase("General", new DateOnly(2026, 6, 1), new DateOnly(2026, 12, 31), general),
                Phase("Semana especial", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7), especial)
            ]
        };
        // El 3 de septiembre, la de inicio más reciente (Semana especial) gana.
        var active = plan.GetActivePhase(new DateOnly(2026, 9, 3));
        Assert.Equal("Semana especial", active!.Name);
        // Fuera de la semana especial, vuelve a regir la General.
        Assert.Equal("General", plan.GetActivePhase(new DateOnly(2026, 10, 1))!.Name);
    }

    [Fact]
    public void OrderedPhases_ordena_por_inicio()
    {
        var plan = new SchedulePlan
        {
            Phases =
            [
                Phase("C", new DateOnly(2027, 3, 1), null),
                Phase("A", new DateOnly(2026, 6, 1), null),
                Phase("B", new DateOnly(2026, 11, 1), null)
            ]
        };
        Assert.Equal(new[] { "A", "B", "C" }, plan.OrderedPhases.Select(p => p.Name).ToArray());
    }
}
