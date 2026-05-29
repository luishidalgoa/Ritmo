using System;
using Ritmo.Core.Commands;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

public class ExportImportTests
{
    private static (ConfigurationService svc, ISettingsStore store) New()
    {
        var store = new InMemorySettingsStore();
        return (new ConfigurationService(store), store);
    }

    [Fact]
    public void Export_luego_Import_reproduce_la_configuracion()
    {
        var (svc, _) = New();
        svc.AddPhase("Fase 1", new DateOnly(2026, 6, 1), null);
        svc.AddSession("Fase 1", new StudySession
        {
            Title = "Técnico", Day = DayOfWeek.Monday, Start = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(2), Kind = StudyKind.Tecnico, PreAlerts = [new PreAlert(10)]
        });
        svc.UpsertEnvironment(new FocusEnvironment { Id = "e1", Name = "Estudio", PomodoroPreset = "DeepWork" });
        svc.SetDefaultEnvironment("e1");

        var json = svc.ExportJson();

        // Importar en un destino limpio reproduce el estado.
        var (svc2, store2) = New();
        var r = svc2.ImportJson(json);
        Assert.True(r.Success);

        var s = store2.Load();
        Assert.Single(s.Plan.Phases);
        Assert.Equal("Fase 1", s.Plan.Phases[0].Name);
        Assert.Single(s.Plan.Phases[0].Schedule.Sessions);
        Assert.Equal("Técnico", s.Plan.Phases[0].Schedule.Sessions[0].Title);
        Assert.Single(s.FocusEnvironments);
        Assert.Equal("e1", s.DefaultFocusEnvironmentId);
    }

    [Fact]
    public void Import_json_invalido_no_cambia_nada()
    {
        var (svc, store) = New();
        svc.AddPhase("Original", new DateOnly(2026, 1, 1), null);

        var r = svc.ImportJson("{ esto no es json válido ");
        Assert.False(r.Success);
        // El estado original sigue intacto.
        Assert.Single(store.Load().Plan.Phases);
        Assert.Equal("Original", store.Load().Plan.Phases[0].Name);
    }

    [Fact]
    public void Import_vacio_falla()
    {
        var (svc, _) = New();
        Assert.False(svc.ImportJson("   ").Success);
    }

    [Fact]
    public void Export_produce_json_no_vacio()
    {
        var (svc, _) = New();
        svc.AddPhase("X", new DateOnly(2026, 1, 1), null);
        var json = svc.ExportJson();
        Assert.Contains("\"phases\"", json);
        Assert.Contains("X", json);
    }
}
