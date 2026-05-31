using System;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

public class PhaseManagementTests
{
    private static ConfigurationService NewSvcWithTwoPhases(out InMemorySettingsStore store)
    {
        store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.AddPhase("Fase 1", new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 31));
        svc.AddPhase("Fase 2", new DateOnly(2026, 6, 1), null);
        return svc;
    }

    [Fact]
    public void AddPhase_rechaza_nombre_duplicado()
    {
        var svc = NewSvcWithTwoPhases(out _);
        Assert.False(svc.AddPhase("Fase 1", new DateOnly(2027, 1, 1), null).Success);
    }

    [Fact]
    public void UpdatePhase_renombra_y_cambia_fechas()
    {
        var svc = NewSvcWithTwoPhases(out var store);
        Assert.True(svc.UpdatePhase("Fase 2", "Segunda vuelta", new DateOnly(2026, 6, 15), new DateOnly(2026, 10, 31)).Success);
        var p = store.Load().Plan.Phases.Single(x => x.Name == "Segunda vuelta");
        Assert.Equal(new DateOnly(2026, 6, 15), p.ValidFrom);
        Assert.Equal(new DateOnly(2026, 10, 31), p.ValidTo);
    }

    [Fact]
    public void UpdatePhase_rechaza_colision_de_nombre()
    {
        var svc = NewSvcWithTwoPhases(out _);
        Assert.False(svc.UpdatePhase("Fase 2", "Fase 1", new DateOnly(2026, 6, 1), null).Success);
    }

    [Fact]
    public void UpdatePhase_fin_antes_de_inicio_falla()
    {
        var svc = NewSvcWithTwoPhases(out _);
        Assert.False(svc.UpdatePhase("Fase 1", "Fase 1", new DateOnly(2026, 5, 1), new DateOnly(2026, 1, 1)).Success);
    }

    [Fact]
    public void RemovePhase_quita_pero_exige_al_menos_una()
    {
        var svc = NewSvcWithTwoPhases(out var store);
        Assert.True(svc.RemovePhase("Fase 2").Success);
        Assert.Single(store.Load().Plan.Phases);
        Assert.False(svc.RemovePhase("Fase 1").Success);   // no se puede quitar la última
        Assert.Single(store.Load().Plan.Phases);
    }

    private static StudySession Block(string title) => new()
    {
        Title = title, Day = DayOfWeek.Monday, Start = new TimeOnly(9, 0),
        Duration = TimeSpan.FromHours(1), CategoryId = "Otro"
    };

    [Fact]
    public void DuplicatePhase_copia_el_horario_a_una_fase_nueva_sin_tocar_la_fuente()
    {
        var svc = NewSvcWithTwoPhases(out var store);
        Assert.True(svc.AddSession("Fase 1", Block("Bloque A")).Success);

        var r = svc.DuplicatePhase("Fase 1", "Fase 1 bis", new DateOnly(2026, 11, 1), null);
        Assert.True(r.Success);

        var phases = store.Load().Plan.Phases;
        var copy = phases.Single(p => p.Name == "Fase 1 bis");
        Assert.Equal(new DateOnly(2026, 11, 1), copy.ValidFrom);
        Assert.Null(copy.ValidTo);
        Assert.Single(copy.Schedule.Sessions);
        Assert.Equal("Bloque A", copy.Schedule.Sessions[0].Title);
        // La fuente queda intacta (1 sesión, sus fechas originales).
        var src = phases.Single(p => p.Name == "Fase 1");
        Assert.Single(src.Schedule.Sessions);
        Assert.Equal(new DateOnly(2026, 1, 1), src.ValidFrom);
    }

    [Fact]
    public void DuplicatePhase_rechaza_fuente_inexistente()
    {
        var svc = NewSvcWithTwoPhases(out _);
        Assert.False(svc.DuplicatePhase("No existe", "Copia", new DateOnly(2026, 12, 1), null).Success);
    }

    [Fact]
    public void DuplicatePhase_rechaza_nombre_que_ya_existe()
    {
        var svc = NewSvcWithTwoPhases(out _);
        Assert.False(svc.DuplicatePhase("Fase 1", "Fase 2", new DateOnly(2026, 12, 1), null).Success);
    }

    [Fact]
    public void DuplicatePhase_rechaza_fin_antes_de_inicio()
    {
        var svc = NewSvcWithTwoPhases(out _);
        Assert.False(svc.DuplicatePhase("Fase 1", "Copia", new DateOnly(2026, 12, 1), new DateOnly(2026, 1, 1)).Success);
    }
}
