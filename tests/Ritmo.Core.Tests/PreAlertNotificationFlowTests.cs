using System;
using System.Collections.Generic;
using Ritmo.Core.Background;
using Ritmo.Core.Model;
using Ritmo.Core.Notifications;
using Ritmo.Core.Scheduling;
using Ritmo.Core.Timing;

namespace Ritmo.Core.Tests;

/// <summary>
/// Prueba la cadena completa que arma ScheduleHost en la app, pero de forma
/// DETERMINISTA: horario → ScheduleRunner (con reloj/scheduler manuales) → EventDue
/// → NotificationBuilder → mensaje de toast. Sin tiempo real ni SO.
/// </summary>
public class PreAlertNotificationFlowTests
{
    private static (ManualClock clock, ManualScheduler sched) Doubles(DateTime start)
    {
        var clock = new ManualClock(start);
        return (clock, new ManualScheduler(clock));
    }

    [Fact]
    public void Aviso_previo_y_inicio_producen_toasts_en_su_momento()
    {
        // Lunes a las 09:00, con avisos 10 y 5 min antes.
        var session = new StudySession
        {
            Title = "Bloque B.II",
            Day = DayOfWeek.Monday,
            Start = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(2),
            CategoryId = "Tecnico",
            PreAlerts = [new PreAlert(10), new PreAlert(5)]
        };
        var schedule = new WeeklySchedule { Sessions = [session] };

        // Arrancamos el lunes a las 08:00.
        var monday8 = new DateTime(2026, 6, 1, 8, 0, 0); // 2026-06-01 es lunes
        var (clock, sched) = Doubles(monday8);

        // Esto es exactamente lo que hace ScheduleHost: EventDue -> NotificationBuilder
        // con el nombre legible de la categoría (#83).
        var toasts = new List<(DateTime At, NotificationMessage Msg)>();
        using var runner = new ScheduleRunner(schedule, clock, sched);
        runner.EventDue += ev => toasts.Add((clock.Now,
            NotificationBuilder.ForEvent(ev, Ritmo.Core.Model.LegacyCategories.ById[ev.Session.CategoryId].Name)));
        runner.Start();

        // Avanzamos hasta pasado el inicio.
        sched.AdvanceTo(new DateTime(2026, 6, 1, 9, 0, 1));

        // 3 toasts: aviso 10', aviso 5', inicio.
        Assert.Equal(3, toasts.Count);

        Assert.Equal(new DateTime(2026, 6, 1, 8, 50, 0), toasts[0].At);
        Assert.Equal("Tu sesión empieza en 10 minutos", toasts[0].Msg.Title);

        Assert.Equal(new DateTime(2026, 6, 1, 8, 55, 0), toasts[1].At);
        Assert.Equal("Tu sesión empieza en 5 minutos", toasts[1].Msg.Title);

        Assert.Equal(new DateTime(2026, 6, 1, 9, 0, 0), toasts[2].At);
        Assert.Equal("Es la hora de concentrarte", toasts[2].Msg.Title);
        Assert.Contains("Técnico", toasts[2].Msg.Body);
    }

    [Fact]
    public void Sesion_tentativa_no_dispara_inicio_pero_si_su_aviso()
    {
        // Un hueco "Por definir" tentativo: su aviso suena (recordatorio suave),
        // pero NO genera evento de inicio de concentración.
        var session = new StudySession
        {
            Title = "Hueco",
            Day = DayOfWeek.Monday,
            Start = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(1),
            CategoryId = "PorDefinir",
            IsTentative = true,
            PreAlerts = [new PreAlert(10)]
        };
        var schedule = new WeeklySchedule { Sessions = [session] };
        var (clock, sched) = Doubles(new DateTime(2026, 6, 1, 8, 0, 0));

        var msgs = new List<NotificationMessage>();
        using var runner = new ScheduleRunner(schedule, clock, sched);
        runner.EventDue += ev => msgs.Add(NotificationBuilder.ForEvent(ev));
        runner.Start();
        sched.AdvanceTo(new DateTime(2026, 6, 1, 9, 0, 1));

        // Solo el aviso previo; ningún "Es la hora de concentrarte".
        Assert.Single(msgs);
        Assert.StartsWith("Tu sesión empieza", msgs[0].Title);
    }
}
