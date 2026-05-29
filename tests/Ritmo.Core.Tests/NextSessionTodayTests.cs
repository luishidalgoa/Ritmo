using System;
using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

public class NextSessionTodayTests
{
    private static WeeklySchedule Schedule() => new()
    {
        Sessions =
        [
            new StudySession { Title = "Mañana técnico", Day = DayOfWeek.Monday, Start = new TimeOnly(9, 0),
                Duration = TimeSpan.FromHours(2), Kind = StudyKind.Tecnico },
            new StudySession { Title = "Comida", Day = DayOfWeek.Monday, Start = new TimeOnly(14, 0),
                Duration = TimeSpan.FromHours(1), Kind = StudyKind.Personal },
            new StudySession { Title = "Tarde legislación", Day = DayOfWeek.Monday, Start = new TimeOnly(17, 0),
                Duration = TimeSpan.FromHours(2), Kind = StudyKind.Legislacion },
            new StudySession { Title = "Inglés (otro día)", Day = DayOfWeek.Thursday, Start = new TimeOnly(9, 0),
                Duration = TimeSpan.FromHours(2), Kind = StudyKind.Ingles },
        ]
    };

    private static SchedulePlanner Planner() => new(Schedule());

    [Fact]
    public void Devuelve_la_siguiente_de_hoy_por_hora()
    {
        // Lunes 10:30 (dentro del bloque de la mañana) -> lo siguiente es la comida 14:00.
        var next = Planner().GetNextSessionToday(new DateTime(2026, 6, 1, 10, 30, 0));
        Assert.NotNull(next);
        Assert.Equal("Comida", next!.Title);
    }

    [Fact]
    public void Incluye_cualquier_tipo_no_solo_concentracion()
    {
        // Lunes 08:00 -> la primera es el bloque técnico de las 09:00.
        var next = Planner().GetNextSessionToday(new DateTime(2026, 6, 1, 8, 0, 0));
        Assert.Equal("Mañana técnico", next!.Title);
    }

    [Fact]
    public void Null_si_no_queda_nada_hoy()
    {
        // Lunes 19:30 -> ya pasó todo lo del lunes.
        Assert.Null(Planner().GetNextSessionToday(new DateTime(2026, 6, 1, 19, 30, 0)));
    }

    [Fact]
    public void Solo_mira_el_dia_actual()
    {
        // Miércoles: no hay nada ese día aunque el jueves sí.
        Assert.Null(Planner().GetNextSessionToday(new DateTime(2026, 6, 3, 8, 0, 0)));
    }

    [Fact]
    public void Estrictamente_posterior_a_ahora()
    {
        // Justo a las 14:00 (inicio de la comida) -> la siguiente es legislación 17:00.
        var next = Planner().GetNextSessionToday(new DateTime(2026, 6, 1, 14, 0, 0));
        Assert.Equal("Tarde legislación", next!.Title);
    }
}
