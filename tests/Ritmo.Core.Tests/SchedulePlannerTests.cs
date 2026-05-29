using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

public class SchedulePlannerTests
{
    // Horario de ejemplo: Lunes 09:00 Técnico (2h) con avisos 60 y 10 min antes,
    // y Jueves 09:00 Inglés (2h) sin avisos.
    private static WeeklySchedule SampleSchedule() => new()
    {
        Sessions =
        [
            new StudySession
            {
                Title = "Técnico ▸ siguiente tema",
                Day = DayOfWeek.Monday,
                Start = new TimeOnly(9, 0),
                Duration = TimeSpan.FromHours(2),
                Kind = StudyKind.Tecnico,
                PreAlerts = [PreAlert.OneHour, PreAlert.TenMinutes]
            },
            new StudySession
            {
                Title = "Inglés — clase",
                Day = DayOfWeek.Thursday,
                Start = new TimeOnly(9, 0),
                Duration = TimeSpan.FromHours(2),
                Kind = StudyKind.Ingles,
                PreAlerts = []
            }
        ]
    };

    // Lunes 1 de junio de 2026 (lo elegimos para tener un lunes real).
    private static readonly DateTime MondayMidnight = new(2026, 6, 1, 0, 0, 0);

    [Fact]
    public void SessionStart_se_genera_a_la_hora_exacta()
    {
        var planner = new SchedulePlanner(SampleSchedule());
        var events = planner.GetEvents(MondayMidnight, TimeSpan.FromDays(1));

        var start = events.Single(e => e.Type == PlannedEventType.SessionStart
                                       && e.Session.Kind == StudyKind.Tecnico);
        Assert.Equal(new DateTime(2026, 6, 1, 9, 0, 0), start.At);
    }

    [Fact]
    public void Genera_un_aviso_por_cada_PreAlert_en_el_momento_correcto()
    {
        var planner = new SchedulePlanner(SampleSchedule());
        var events = planner.GetEvents(MondayMidnight, TimeSpan.FromDays(1));

        var alerts = events.Where(e => e.Type == PlannedEventType.PreAlert).ToList();
        Assert.Equal(2, alerts.Count);

        // 60 min antes de las 09:00 -> 08:00
        Assert.Contains(alerts, a => a.MinutesBefore == 60 && a.At == new DateTime(2026, 6, 1, 8, 0, 0));
        // 10 min antes de las 09:00 -> 08:50
        Assert.Contains(alerts, a => a.MinutesBefore == 10 && a.At == new DateTime(2026, 6, 1, 8, 50, 0));
    }

    [Fact]
    public void Los_eventos_se_devuelven_ordenados_por_tiempo()
    {
        var planner = new SchedulePlanner(SampleSchedule());
        var events = planner.GetEvents(MondayMidnight, TimeSpan.FromDays(1));

        var times = events.Select(e => e.At).ToList();
        var sorted = times.OrderBy(t => t).ToList();
        Assert.Equal(sorted, times);
    }

    [Fact]
    public void A_igual_hora_el_aviso_va_antes_que_el_inicio()
    {
        // Sesión con un aviso "0 min antes" (mismo instante que el inicio).
        var schedule = new WeeklySchedule
        {
            Sessions =
            [
                new StudySession
                {
                    Title = "Borde",
                    Day = DayOfWeek.Monday,
                    Start = new TimeOnly(9, 0),
                    Duration = TimeSpan.FromHours(1),
                    PreAlerts = [new PreAlert(0)]
                }
            ]
        };
        var planner = new SchedulePlanner(schedule);
        var events = planner.GetEvents(MondayMidnight, TimeSpan.FromDays(1));

        Assert.Equal(PlannedEventType.PreAlert, events[0].Type);
        Assert.Equal(PlannedEventType.SessionStart, events[1].Type);
    }

    [Fact]
    public void GetNextEvent_devuelve_el_inmediatamente_posterior()
    {
        var planner = new SchedulePlanner(SampleSchedule());
        // A las 08:30 del lunes, el siguiente evento debe ser el aviso de 10 min (08:50).
        var now = new DateTime(2026, 6, 1, 8, 30, 0);
        var next = planner.GetNextEvent(now);

        Assert.NotNull(next);
        Assert.Equal(PlannedEventType.PreAlert, next!.Type);
        Assert.Equal(10, next.MinutesBefore);
        Assert.Equal(new DateTime(2026, 6, 1, 8, 50, 0), next.At);
    }

    [Fact]
    public void GetNextEvent_cruza_al_siguiente_dia_con_sesiones()
    {
        var planner = new SchedulePlanner(SampleSchedule());
        // Martes (sin sesiones): el siguiente evento es el jueves 09:00 (Inglés, sin avisos).
        var tuesday = new DateTime(2026, 6, 2, 12, 0, 0);
        var next = planner.GetNextEvent(tuesday);

        Assert.NotNull(next);
        Assert.Equal(StudyKind.Ingles, next!.Session.Kind);
        Assert.Equal(new DateTime(2026, 6, 4, 9, 0, 0), next.At); // jueves
    }

    [Fact]
    public void GetActiveSession_detecta_sesion_en_curso()
    {
        var planner = new SchedulePlanner(SampleSchedule());
        // Lunes 10:00 está dentro de 09:00–11:00.
        var active = planner.GetActiveSession(new DateTime(2026, 6, 1, 10, 0, 0));
        Assert.NotNull(active);
        Assert.Equal(StudyKind.Tecnico, active!.Kind);
    }

    [Fact]
    public void GetActiveSession_es_null_fuera_de_horario()
    {
        var planner = new SchedulePlanner(SampleSchedule());
        // Lunes 11:00 ya es el fin exacto (exclusivo) -> no activa.
        Assert.Null(planner.GetActiveSession(new DateTime(2026, 6, 1, 11, 0, 0)));
        // Lunes 07:00 -> antes de empezar.
        Assert.Null(planner.GetActiveSession(new DateTime(2026, 6, 1, 7, 0, 0)));
    }

    [Fact]
    public void El_inicio_de_sesion_es_inclusivo_y_el_fin_exclusivo()
    {
        var planner = new SchedulePlanner(SampleSchedule());
        // Exactamente a las 09:00 ya está activa.
        Assert.NotNull(planner.GetActiveSession(new DateTime(2026, 6, 1, 9, 0, 0)));
    }

    [Fact]
    public void Sesion_que_cruza_medianoche_se_detecta_en_ambos_tramos()
    {
        var schedule = new WeeklySchedule
        {
            Sessions =
            [
                new StudySession
                {
                    Title = "Nocturna",
                    Day = DayOfWeek.Monday,
                    Start = new TimeOnly(23, 0),
                    Duration = TimeSpan.FromHours(2), // 23:00 -> 01:00
                    Kind = StudyKind.Otro
                }
            ]
        };
        var planner = new SchedulePlanner(schedule);
        // 23:30 del lunes: dentro.
        Assert.NotNull(planner.GetActiveSession(new DateTime(2026, 6, 1, 23, 30, 0)));
        // 00:30 del lunes (madrugada del mismo DayOfWeek): dentro del tramo posterior.
        Assert.NotNull(planner.GetActiveSession(new DateTime(2026, 6, 1, 0, 30, 0)));
    }

    [Fact]
    public void Horizonte_vacio_no_genera_eventos()
    {
        var planner = new SchedulePlanner(SampleSchedule());
        // Un martes con horizonte de 1 día no contiene sesiones.
        var events = planner.GetEvents(new DateTime(2026, 6, 2, 0, 0, 0), TimeSpan.FromHours(20));
        Assert.Empty(events);
    }
}
