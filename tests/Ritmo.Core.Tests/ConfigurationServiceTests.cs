using Ritmo.Core.Commands;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

/// <summary>Store en memoria para testear el servicio sin tocar disco.</summary>
internal sealed class InMemorySettingsStore : ISettingsStore
{
    private AppSettings _current = AppSettings.Default;
    public AppSettings Load() => _current;
    public void Save(AppSettings settings) => _current = settings;
}

public class ConfigurationServiceTests
{
    private static (ConfigurationService, InMemorySettingsStore) New()
    {
        var store = new InMemorySettingsStore();
        return (new ConfigurationService(store), store);
    }

    [Fact]
    public void AddPhase_anade_y_persiste()
    {
        var (svc, store) = New();
        var r = svc.AddPhase("Fase 1", new DateOnly(2026, 6, 1), new DateOnly(2026, 10, 31));
        Assert.True(r.Success);
        Assert.Single(store.Load().Plan.Phases);
    }

    [Fact]
    public void AddPhase_rechaza_nombre_vacio_y_fechas_invertidas()
    {
        var (svc, _) = New();
        Assert.False(svc.AddPhase("  ", new DateOnly(2026, 6, 1), null).Success);
        Assert.False(svc.AddPhase("X", new DateOnly(2026, 10, 1), new DateOnly(2026, 6, 1)).Success);
    }

    [Fact]
    public void AddPhase_rechaza_nombre_duplicado()
    {
        var (svc, _) = New();
        svc.AddPhase("Fase 1", new DateOnly(2026, 6, 1), null);
        var r = svc.AddPhase("fase 1", new DateOnly(2027, 1, 1), null); // case-insensitive
        Assert.False(r.Success);
    }

    [Fact]
    public void AddSession_a_fase_existente()
    {
        var (svc, store) = New();
        svc.AddPhase("Fase 1", new DateOnly(2026, 6, 1), null);
        var session = new StudySession
        {
            Title = "Técnico", Day = DayOfWeek.Monday,
            Start = new TimeOnly(9, 0), Duration = TimeSpan.FromHours(2), Kind = StudyKind.Tecnico
        };
        var r = svc.AddSession("Fase 1", session);
        Assert.True(r.Success);
        Assert.Single(store.Load().Plan.Phases[0].Schedule.Sessions);
    }

    [Fact]
    public void AddSession_falla_si_la_fase_no_existe()
    {
        var (svc, _) = New();
        var s = new StudySession { Title = "X", Day = DayOfWeek.Monday, Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(1) };
        Assert.False(svc.AddSession("Inexistente", s).Success);
    }

    [Fact]
    public void AddSession_valida_duracion_y_titulo()
    {
        var (svc, _) = New();
        svc.AddPhase("F", new DateOnly(2026, 6, 1), null);
        var sinDuracion = new StudySession { Title = "X", Day = DayOfWeek.Monday, Start = new TimeOnly(9,0), Duration = TimeSpan.Zero };
        Assert.False(svc.AddSession("F", sinDuracion).Success);
    }

    [Fact]
    public void UpdateSession_reemplaza_por_indice()
    {
        var (svc, store) = New();
        svc.AddPhase("F", new DateOnly(2026, 6, 1), null);
        var s1 = new StudySession { Title = "A", Day = DayOfWeek.Monday, Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(1) };
        svc.AddSession("F", s1);
        var s2 = s1 with { Title = "A-editada", Duration = TimeSpan.FromHours(2) };
        var r = svc.UpdateSession("F", 0, s2);
        Assert.True(r.Success);
        var saved = store.Load().Plan.Phases[0].Schedule.Sessions[0];
        Assert.Equal("A-editada", saved.Title);
        Assert.Equal(TimeSpan.FromHours(2), saved.Duration);
    }

    [Fact]
    public void UpdateSession_indice_invalido_falla()
    {
        var (svc, _) = New();
        svc.AddPhase("F", new DateOnly(2026, 6, 1), null);
        var s = new StudySession { Title = "X", Day = DayOfWeek.Monday, Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(1) };
        Assert.False(svc.UpdateSession("F", 5, s).Success);
    }

    [Fact]
    public void RemoveSession_borra_por_indice()
    {
        var (svc, store) = New();
        svc.AddPhase("F", new DateOnly(2026, 6, 1), null);
        svc.AddSession("F", new StudySession { Title = "A", Day = DayOfWeek.Monday, Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(1) });
        svc.AddSession("F", new StudySession { Title = "B", Day = DayOfWeek.Tuesday, Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(1) });
        var r = svc.RemoveSession("F", 0);
        Assert.True(r.Success);
        var sessions = store.Load().Plan.Phases[0].Schedule.Sessions;
        Assert.Single(sessions);
        Assert.Equal("B", sessions[0].Title);
    }

    [Fact]
    public void RemoveSession_indice_invalido_falla()
    {
        var (svc, _) = New();
        svc.AddPhase("F", new DateOnly(2026, 6, 1), null);
        Assert.False(svc.RemoveSession("F", 0).Success);
    }

    [Fact]
    public void UpsertEnvironment_crea_y_actualiza()
    {
        var (svc, store) = New();
        svc.UpsertEnvironment(new FocusEnvironment { Id = "deep", Name = "Profundo" });
        Assert.Single(store.Load().FocusEnvironments);

        // Mismo Id -> reemplaza, no duplica.
        svc.UpsertEnvironment(new FocusEnvironment { Id = "deep", Name = "Profundo v2" });
        var envs = store.Load().FocusEnvironments;
        Assert.Single(envs);
        Assert.Equal("Profundo v2", envs[0].Name);
    }

    [Fact]
    public void SetDefaultEnvironment_exige_que_exista()
    {
        var (svc, store) = New();
        Assert.False(svc.SetDefaultEnvironment("nope").Success);
        svc.UpsertEnvironment(new FocusEnvironment { Id = "deep", Name = "Profundo" });
        Assert.True(svc.SetDefaultEnvironment("deep").Success);
        Assert.Equal("deep", store.Load().DefaultFocusEnvironmentId);
    }

    [Fact]
    public void MapEnvironmentToKind_asocia_tipo_a_entorno()
    {
        var (svc, store) = New();
        svc.UpsertEnvironment(new FocusEnvironment { Id = "sim", Name = "Simulacro" });
        var r = svc.MapEnvironmentToKind(StudyKind.Simulacro, "sim");
        Assert.True(r.Success);
        Assert.Equal("sim", store.Load().ResolveEnvironment(StudyKind.Simulacro)!.Id);
    }

    [Fact]
    public void SetPomodoro_actualiza_y_valida()
    {
        var (svc, store) = New();
        Assert.True(svc.SetPomodoro(50, 10, 20, 2).Success);
        Assert.Equal(TimeSpan.FromMinutes(50), store.Load().Pomodoro.Focus);
        // Inválidos:
        Assert.False(svc.SetPomodoro(0, 5, 15, 4).Success);
        Assert.False(svc.SetPomodoro(25, 5, 15, 0).Success);
    }

    [Fact]
    public void SetViewHours_actualiza_y_valida()
    {
        var (svc, store) = New();
        Assert.True(svc.SetViewHours(new TimeOnly(7, 0), new TimeOnly(22, 0)).Success);
        Assert.Equal(new TimeOnly(7, 0), store.Load().ViewConfig.DayStart);
        Assert.Equal(new TimeOnly(22, 0), store.Load().ViewConfig.DayEnd);
        // Fin antes que inicio -> falla.
        Assert.False(svc.SetViewHours(new TimeOnly(20, 0), new TimeOnly(8, 0)).Success);
    }

    [Fact]
    public void GetStatus_resume_el_estado()
    {
        var (svc, _) = New();
        svc.AddPhase("Fase 1", new DateOnly(2026, 6, 1), null);
        svc.UpsertEnvironment(new FocusEnvironment { Id = "deep", Name = "Profundo" });
        svc.SetDefaultEnvironment("deep");

        var status = svc.GetStatus();
        Assert.Equal(1, status.PhaseCount);
        Assert.Contains("Fase 1", status.PhaseNames);
        Assert.Equal(1, status.EnvironmentCount);
        Assert.Equal("deep", status.DefaultEnvironmentId);
    }
}
