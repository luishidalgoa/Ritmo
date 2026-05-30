using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

public class EnvironmentManagementTests
{
    private static (ConfigurationService svc, ISettingsStore store) New()
    {
        var store = new InMemorySettingsStore();
        return (new ConfigurationService(store), store);
    }

    private static FocusEnvironment Env(string id, string name) =>
        new() { Id = id, Name = name, PomodoroPreset = "DeepWork" };

    [Fact]
    public void Upsert_crea_y_luego_reemplaza_por_id()
    {
        var (svc, store) = New();
        Assert.True(svc.UpsertEnvironment(Env("e1", "Estudio")).Success);
        Assert.Single(store.Load().FocusEnvironments);

        // Mismo id -> reemplaza, no duplica.
        Assert.True(svc.UpsertEnvironment(Env("e1", "Estudio profundo")).Success);
        var envs = store.Load().FocusEnvironments;
        Assert.Single(envs);
        Assert.Equal("Estudio profundo", envs[0].Name);
    }

    [Fact]
    public void Remove_borra_el_entorno()
    {
        var (svc, store) = New();
        svc.UpsertEnvironment(Env("e1", "A"));
        svc.UpsertEnvironment(Env("e2", "B"));

        Assert.True(svc.RemoveEnvironment("e1").Success);
        var envs = store.Load().FocusEnvironments;
        Assert.Single(envs);
        Assert.Equal("e2", envs[0].Id);
    }

    [Fact]
    public void Remove_inexistente_falla()
    {
        var (svc, _) = New();
        Assert.False(svc.RemoveEnvironment("nope").Success);
    }

    [Fact]
    public void Remove_del_por_defecto_lo_deja_sin_por_defecto()
    {
        var (svc, store) = New();
        svc.UpsertEnvironment(Env("e1", "A"));
        svc.SetDefaultEnvironment("e1");
        Assert.Equal("e1", store.Load().DefaultFocusEnvironmentId);

        svc.RemoveEnvironment("e1");
        Assert.Null(store.Load().DefaultFocusEnvironmentId);
    }

    [Fact]
    public void Remove_limpia_los_mapeos_por_tipo()
    {
        var (svc, store) = New();
        svc.UpsertEnvironment(Env("e1", "A"));
        svc.MapEnvironmentToKind(StudyKind.Simulacro, "e1");
        Assert.Contains("e1", store.Load().EnvironmentByKind.Values);

        svc.RemoveEnvironment("e1");
        Assert.DoesNotContain("e1", store.Load().EnvironmentByKind.Values);
    }

    [Fact]
    public void SetDefault_inexistente_falla()
    {
        var (svc, _) = New();
        Assert.False(svc.SetDefaultEnvironment("nope").Success);
    }

    [Fact]
    public void SetDefault_null_o_vacio_limpia_la_seleccion()
    {
        var (svc, store) = New();
        svc.UpsertEnvironment(Env("e1", "A"));
        svc.SetDefaultEnvironment("e1");
        Assert.Equal("e1", store.Load().DefaultFocusEnvironmentId);

        Assert.True(svc.SetDefaultEnvironment(null).Success);
        Assert.Null(store.Load().DefaultFocusEnvironmentId);

        svc.SetDefaultEnvironment("e1");
        Assert.True(svc.SetDefaultEnvironment("").Success);
        Assert.Null(store.Load().DefaultFocusEnvironmentId);
    }
}
