using System;
using System.Linq;
using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

public class SessionMergeTests
{
    private static readonly DayOfWeek[] Days =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
          DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

    private static StudySession S(string title, DayOfWeek day, int hour = 9, double hours = 1, string kind = "Descanso")
        => new() { Title = title, Day = day, Start = new TimeOnly(hour, 0), Duration = TimeSpan.FromHours(hours), CategoryId = kind };

    [Fact]
    public void Dias_contiguos_identicos_se_fusionan()
    {
        var sessions = new[]
        {
            S("Descanso", DayOfWeek.Monday),
            S("Descanso", DayOfWeek.Tuesday),
            S("Descanso", DayOfWeek.Wednesday),
        };
        var groups = SessionMerge.Merge(sessions, Days);
        Assert.Single(groups);
        Assert.Equal(0, groups[0].FirstDayIndex);
        Assert.Equal(3, groups[0].DaySpan);
        Assert.Equal(3, groups[0].Members.Count);
    }

    [Fact]
    public void Dias_no_contiguos_no_se_fusionan()
    {
        var sessions = new[] { S("Descanso", DayOfWeek.Monday), S("Descanso", DayOfWeek.Wednesday) };
        var groups = SessionMerge.Merge(sessions, Days);
        Assert.Equal(2, groups.Count);
        Assert.All(groups, g => Assert.Equal(1, g.DaySpan));
    }

    [Fact]
    public void Distinta_hora_no_fusiona()
    {
        var sessions = new[] { S("Descanso", DayOfWeek.Monday, hour: 9), S("Descanso", DayOfWeek.Tuesday, hour: 10) };
        var groups = SessionMerge.Merge(sessions, Days);
        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void Distinto_titulo_no_fusiona()
    {
        var sessions = new[] { S("Técnico", DayOfWeek.Monday, kind: "Tecnico"), S("Legislación", DayOfWeek.Tuesday, kind: "Legislacion") };
        var groups = SessionMerge.Merge(sessions, Days);
        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void Dos_runs_separados_de_la_misma_firma()
    {
        // Lun-Mar contiguos + Vie suelto, todos "Descanso" -> 2 grupos.
        var sessions = new[]
        {
            S("Descanso", DayOfWeek.Monday), S("Descanso", DayOfWeek.Tuesday),
            S("Descanso", DayOfWeek.Friday),
        };
        var groups = SessionMerge.Merge(sessions, Days).OrderBy(g => g.FirstDayIndex).ToList();
        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups[0].DaySpan);   // Lun-Mar
        Assert.Equal(1, groups[1].DaySpan);   // Vie
        Assert.Equal(4, groups[1].FirstDayIndex);
    }

    [Fact]
    public void Sesion_unica_es_grupo_de_uno()
    {
        var groups = SessionMerge.Merge(new[] { S("X", DayOfWeek.Thursday) }, Days);
        Assert.Single(groups);
        Assert.Equal(1, groups[0].DaySpan);
        Assert.Equal(3, groups[0].FirstDayIndex);
    }

    [Fact]
    public void Lista_vacia()
        => Assert.Empty(SessionMerge.Merge(Array.Empty<StudySession>(), Days));
}
