using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

public class TentativeSessionTests
{
    private static readonly DateTime MondayMidnight = new(2026, 6, 1, 0, 0, 0);

    [Fact]
    public void Sesion_normal_dispara_inicio()
    {
        var plan = new SchedulePlanner(new WeeklySchedule
        {
            Sessions = [ new StudySession {
                Title = "Firme", Day = DayOfWeek.Monday,
                Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(2) } ]
        });
        var starts = plan.GetEvents(MondayMidnight, TimeSpan.FromDays(1))
                         .Where(e => e.Type == PlannedEventType.SessionStart);
        Assert.Single(starts);
    }

    [Fact]
    public void Bloque_tentativo_NO_dispara_inicio()
    {
        var plan = new SchedulePlanner(new WeeklySchedule
        {
            Sessions = [ new StudySession {
                Title = "Quizá", Day = DayOfWeek.Monday,
                Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(2),
                IsTentative = true } ]
        });
        var starts = plan.GetEvents(MondayMidnight, TimeSpan.FromDays(1))
                         .Where(e => e.Type == PlannedEventType.SessionStart);
        Assert.Empty(starts);
    }

    [Fact]
    public void Bloque_tentativo_SI_emite_sus_avisos()
    {
        var plan = new SchedulePlanner(new WeeklySchedule
        {
            Sessions = [ new StudySession {
                Title = "Quizá", Day = DayOfWeek.Monday,
                Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(2),
                IsTentative = true, PreAlerts = [ PreAlert.TenMinutes ] } ]
        });
        var events = plan.GetEvents(MondayMidnight, TimeSpan.FromDays(1));
        // No hay inicio, pero sí el aviso (recordatorio suave).
        Assert.DoesNotContain(events, e => e.Type == PlannedEventType.SessionStart);
        var alert = Assert.Single(events.Where(e => e.Type == PlannedEventType.PreAlert));
        Assert.Equal(new DateTime(2026, 6, 1, 8, 50, 0), alert.At);
    }

    [Fact]
    public void GetActiveSession_ignora_los_tentativos()
    {
        var plan = new SchedulePlanner(new WeeklySchedule
        {
            Sessions = [ new StudySession {
                Title = "Quizá", Day = DayOfWeek.Monday,
                Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(2),
                IsTentative = true } ]
        });
        // A las 10:00 (dentro del horario) no hay sesión "activa" que disparar concentración.
        Assert.Null(plan.GetActiveSession(new DateTime(2026, 6, 1, 10, 0, 0)));
    }

    [Fact]
    public void Tipo_PorDefinir_existe_y_es_asignable()
    {
        var s = new StudySession
        {
            Title = "Hueco de estudio",
            Day = DayOfWeek.Tuesday,
            Start = new TimeOnly(16, 0),
            Duration = TimeSpan.FromHours(2),
            Kind = StudyKind.PorDefinir
        };
        Assert.Equal(StudyKind.PorDefinir, s.Kind);
        Assert.False(s.IsTentative); // "Por definir" no implica tentativo: es un hueco firme sin materia.
    }

    [Fact]
    public void IsTentative_por_defecto_es_false()
    {
        var s = new StudySession
        {
            Title = "X", Day = DayOfWeek.Monday,
            Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(1)
        };
        Assert.False(s.IsTentative);
    }
}
