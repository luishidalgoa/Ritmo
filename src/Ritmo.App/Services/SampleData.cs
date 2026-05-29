using Ritmo.Core.Model;

namespace Ritmo_App.Services;

/// <summary>Horario de ejemplo (Fase 1 del TAI) para sembrar la primera vez.</summary>
internal static class SampleData
{
    public static SchedulePlan TaiPlan()
    {
        StudySession S(string title, DayOfWeek day, int h, double hours, StudyKind kind,
                       params int[] alerts) => new()
        {
            Title = title, Day = day, Start = new TimeOnly(h, 0),
            Duration = TimeSpan.FromHours(hours), Kind = kind,
            PreAlerts = alerts.Select(a => new PreAlert(a)).ToList()
        };

        var sessions = new List<StudySession>();
        foreach (var d in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                                  DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            // Mañana: 9-11 (jueves = inglés), 12-14 técnico
            if (d == DayOfWeek.Thursday)
                sessions.Add(S("Inglés — clase", d, 9, 2, StudyKind.Ingles, 60, 10));
            else if (d is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday)
                sessions.Add(S("Legislación (B.I)", d, 9, 2, StudyKind.Legislacion));
            else
                sessions.Add(S("Técnico", d, 9, 2, StudyKind.Tecnico));

            sessions.Add(S("Técnico", d, 12, 2, StudyKind.Tecnico));
            // Tarde: tests / ofimática
            sessions.Add(S(d == DayOfWeek.Thursday ? "Práctica" : "Tests del tema", d, 16, 2, StudyKind.Tests));
        }

        return new SchedulePlan
        {
            Phases =
            [
                new SchedulePhase
                {
                    Name = "Fase 1 · Primera vuelta",
                    ValidFrom = new DateOnly(2026, 6, 1),
                    ValidTo = new DateOnly(2026, 10, 31),
                    Schedule = new WeeklySchedule { Sessions = sessions }
                }
            ]
        };
    }
}
