using System;
using System.Linq;
using Ritmo.Core.Model;
using Ritmo.Core.Sync;

namespace Ritmo.Core.Tests;

public class CalendarPublishTests
{
    private static StudySession Sess(string title, DayOfWeek day, int hour, double durH = 2) => new()
    {
        Title = title, Day = day, Start = new TimeOnly(hour, 0), Duration = TimeSpan.FromHours(durH), CategoryId = "x"
    };

    private static SchedulePlan Plan(params SchedulePhase[] phases) => new() { Phases = phases };

    [Fact]
    public void FirstOccurrence_devuelve_el_mismo_dia_o_el_siguiente()
    {
        var mon = new DateOnly(2026, 6, 1);   // lunes
        Assert.Equal(mon, CalendarPublish.FirstOccurrence(mon, DayOfWeek.Monday));            // mismo día
        Assert.Equal(new DateOnly(2026, 6, 2), CalendarPublish.FirstOccurrence(mon, DayOfWeek.Tuesday));
        Assert.Equal(new DateOnly(2026, 6, 7), CalendarPublish.FirstOccurrence(mon, DayOfWeek.Sunday));
    }

    [Fact]
    public void Sesion_recurrente_genera_evento_semanal_con_until_y_primera_ocurrencia()
    {
        var phase = new SchedulePhase
        {
            Name = "Fase 1",
            ValidFrom = new DateOnly(2026, 6, 1),    // lunes
            ValidTo = new DateOnly(2026, 10, 31),
            Schedule = new WeeklySchedule { Sessions = [ Sess("Estudio", DayOfWeek.Wednesday, 16) ] }
        };
        var spec = Assert.Single(CalendarPublish.BuildSpecs(Plan(phase), []));
        Assert.Equal("Estudio", spec.Title);
        Assert.Equal(DayOfWeek.Wednesday, spec.WeeklyOn);
        Assert.Equal(new DateOnly(2026, 6, 3).ToDateTime(new TimeOnly(16, 0)), spec.Start);   // primer miércoles
        Assert.Equal(spec.Start.AddHours(2), spec.End);
        Assert.Equal(new DateOnly(2026, 10, 31), spec.Until);
        Assert.StartsWith("rec|Fase 1|3|Estudio|", spec.Key);
    }

    [Fact]
    public void Fase_sin_ValidTo_recurrencia_indefinida()
    {
        var phase = new SchedulePhase
        {
            Name = "Indef", ValidFrom = new DateOnly(2026, 6, 1), ValidTo = null,
            Schedule = new WeeklySchedule { Sessions = [ Sess("X", DayOfWeek.Monday, 9) ] }
        };
        var spec = Assert.Single(CalendarPublish.BuildSpecs(Plan(phase), []));
        Assert.Null(spec.Until);
        Assert.Equal(DayOfWeek.Monday, spec.WeeklyOn);
    }

    [Fact]
    public void Extraordinaria_genera_evento_unico()
    {
        var one = new OneOffSession
        {
            Id = "o1", Date = new DateOnly(2026, 6, 16), Title = "Clase extra",
            Start = new TimeOnly(18, 0), Duration = TimeSpan.FromHours(1.5)
        };
        var spec = Assert.Single(CalendarPublish.BuildSpecs(Plan(), [one]));
        Assert.Null(spec.WeeklyOn);
        Assert.Null(spec.Until);
        Assert.Equal("one|o1", spec.Key);
        Assert.Equal(new DateOnly(2026, 6, 16).ToDateTime(new TimeOnly(18, 0)), spec.Start);
        Assert.Equal(spec.Start.AddMinutes(90), spec.End);
    }

    [Fact]
    public void Varias_fases_y_extraordinarias_juntas()
    {
        var p1 = new SchedulePhase { Name = "F1", ValidFrom = new DateOnly(2026, 6, 1), ValidTo = new DateOnly(2026, 10, 31),
            Schedule = new WeeklySchedule { Sessions = [ Sess("A", DayOfWeek.Monday, 9), Sess("B", DayOfWeek.Friday, 17) ] } };
        var p2 = new SchedulePhase { Name = "F2", ValidFrom = new DateOnly(2026, 11, 1), ValidTo = null,
            Schedule = new WeeklySchedule { Sessions = [ Sess("C", DayOfWeek.Tuesday, 10) ] } };
        var one = new OneOffSession { Id = "z", Date = new DateOnly(2026, 7, 1), Title = "Extra", Start = new TimeOnly(12, 0), Duration = TimeSpan.FromHours(1) };

        var specs = CalendarPublish.BuildSpecs(Plan(p1, p2), [one]);
        Assert.Equal(4, specs.Count);                                  // 3 recurrentes + 1 extraordinaria
        Assert.Equal(3, specs.Count(s => s.WeeklyOn is not null));
        Assert.Single(specs.Where(s => s.WeeklyOn is null));
        Assert.Equal(specs.Select(s => s.Key).Distinct().Count(), specs.Count);   // claves únicas
    }

    [Fact]
    public void Sesion_de_duracion_cero_se_ignora()
    {
        var phase = new SchedulePhase { Name = "F", ValidFrom = new DateOnly(2026, 6, 1), ValidTo = null,
            Schedule = new WeeklySchedule { Sessions = [ new StudySession { Title = "Z", Day = DayOfWeek.Monday, Start = new TimeOnly(9, 0), Duration = TimeSpan.Zero, CategoryId = "x" } ] } };
        Assert.Empty(CalendarPublish.BuildSpecs(Plan(phase), []));
    }
}
