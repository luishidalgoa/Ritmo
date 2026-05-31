using System;
using Ritmo.Core.Commands;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

public class ReplaceSessionsTests
{
    private static (ConfigurationService svc, ISettingsStore store) New()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.AddPhase("F1", new DateOnly(2026, 1, 1), null);
        return (svc, store);
    }

    private static StudySession S(string t, DayOfWeek d) =>
        new() { Title = t, Day = d, Start = new TimeOnly(9, 0), Duration = TimeSpan.FromHours(1), CategoryId = "Descanso" };

    [Fact]
    public void Reemplaza_la_lista_completa()
    {
        var (svc, store) = New();
        svc.AddSession("F1", S("Viejo", DayOfWeek.Monday));
        var r = svc.ReplaceSessions("F1", [S("Nuevo", DayOfWeek.Tuesday), S("Nuevo", DayOfWeek.Wednesday)]);
        Assert.True(r.Success);
        var sessions = store.Load().Plan.Phases[0].Schedule.Sessions;
        Assert.Equal(2, sessions.Count);
        Assert.DoesNotContain(sessions, x => x.Title == "Viejo");
    }

    [Fact]
    public void Permite_lista_vacia_borra_todo()
    {
        var (svc, store) = New();
        svc.AddSession("F1", S("X", DayOfWeek.Monday));
        Assert.True(svc.ReplaceSessions("F1", Array.Empty<StudySession>()).Success);
        Assert.Empty(store.Load().Plan.Phases[0].Schedule.Sessions);
    }

    [Fact]
    public void Rechaza_sesion_invalida()
    {
        var (svc, _) = New();
        var bad = S("X", DayOfWeek.Monday) with { Duration = TimeSpan.Zero };
        Assert.False(svc.ReplaceSessions("F1", [bad]).Success);
    }

    [Fact]
    public void Fase_inexistente_falla()
    {
        var (svc, _) = New();
        Assert.False(svc.ReplaceSessions("nope", [S("X", DayOfWeek.Monday)]).Success);
    }
}
