using System;
using System.Linq;
using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

/// <summary>
/// El planificador debe generar eventos (inicio + avisos) también para las sesiones
/// provisionales (one-off), no solo para las recurrentes (#128). Antes no se planificaban,
/// así que sus avisos nunca sonaban.
/// </summary>
public class OneOffPlanningTests
{
    private static readonly DateTime From = new(2026, 6, 1, 0, 0, 0);   // lunes

    [Fact]
    public void Genera_inicio_y_aviso_de_una_one_off_de_concentracion()
    {
        var oneOff = new OneOffSession
        {
            Id = "o1", Date = new DateOnly(2026, 6, 3), Title = "Extra",
            Start = new TimeOnly(10, 0), Duration = TimeSpan.FromHours(1),
            CategoryId = "Tecnico",   // focus por defecto
            PreAlerts = [new PreAlert(10)]
        };
        var planner = new SchedulePlanner(new WeeklySchedule(), null, [oneOff]);

        var events = planner.GetEvents(From, TimeSpan.FromDays(7));

        Assert.Contains(events, e => e.Type == PlannedEventType.SessionStart && e.At == new DateTime(2026, 6, 3, 10, 0, 0));
        Assert.Contains(events, e => e.Type == PlannedEventType.PreAlert && e.At == new DateTime(2026, 6, 3, 9, 50, 0));
    }

    [Fact]
    public void Una_one_off_no_focus_no_dispara_inicio_pero_su_aviso_si_suena()
    {
        var oneOff = new OneOffSession
        {
            Id = "o2", Date = new DateOnly(2026, 6, 3), Title = "Cita médica",
            Start = new TimeOnly(10, 0), Duration = TimeSpan.FromHours(1),
            CategoryId = "Otro",   // no dispara concentración
            PreAlerts = [new PreAlert(5)]
        };
        var planner = new SchedulePlanner(new WeeklySchedule(), null, [oneOff]);

        var events = planner.GetEvents(From, TimeSpan.FromDays(7));

        Assert.DoesNotContain(events, e => e.Type == PlannedEventType.SessionStart);
        Assert.Contains(events, e => e.Type == PlannedEventType.PreAlert && e.At == new DateTime(2026, 6, 3, 9, 55, 0));
    }

    [Fact]
    public void Una_one_off_fuera_del_horizonte_no_genera_eventos()
    {
        var oneOff = new OneOffSession
        {
            Id = "o3", Date = new DateOnly(2026, 7, 1), Title = "Lejos",
            Start = new TimeOnly(10, 0), Duration = TimeSpan.FromHours(1),
            CategoryId = "Tecnico", PreAlerts = [new PreAlert(10)]
        };
        var planner = new SchedulePlanner(new WeeklySchedule(), null, [oneOff]);

        var events = planner.GetEvents(From, TimeSpan.FromDays(7));

        Assert.Empty(events);
    }
}
