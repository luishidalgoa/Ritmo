using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Focus;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

public class EnvironmentTasksTests
{
    private static (ConfigurationService svc, ISettingsStore store) New()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.UpsertEnvironment(new FocusEnvironment { Id = "opos", Name = "Oposiciones" });
        return (svc, store);
    }

    private static FocusEnvironment Env(ISettingsStore s) => s.Load().FocusEnvironments.Single();

    [Fact]
    public void AddTask_anade_con_orden_incremental()
    {
        var (svc, store) = New();
        svc.AddEnvironmentTask("opos", "Repasar Redes");
        svc.AddEnvironmentTask("opos", "Hacer test B.II");
        var tasks = Env(store).Tasks;
        Assert.Equal(2, tasks.Count);
        Assert.True(tasks[1].Order > tasks[0].Order);
        Assert.False(tasks[0].Done);
    }

    [Fact]
    public void AddTask_valida_texto_y_entorno()
    {
        var (svc, _) = New();
        Assert.False(svc.AddEnvironmentTask("opos", "   ").Success);
        Assert.False(svc.AddEnvironmentTask("nope", "x").Success);
    }

    [Fact]
    public void ToggleTask_marca_y_desmarca()
    {
        var (svc, store) = New();
        var id = svc.AddEnvironmentTask("opos", "X").Message;
        Assert.True(svc.ToggleEnvironmentTask("opos", id).Success);
        Assert.True(Env(store).Tasks.Single().Done);
        svc.ToggleEnvironmentTask("opos", id);
        Assert.False(Env(store).Tasks.Single().Done);
    }

    [Fact]
    public void RemoveTask_borra()
    {
        var (svc, store) = New();
        var id = svc.AddEnvironmentTask("opos", "X").Message;
        Assert.True(svc.RemoveEnvironmentTask("opos", id).Success);
        Assert.Empty(Env(store).Tasks);
    }

    [Fact]
    public void Toggle_y_Remove_de_tarea_inexistente_fallan()
    {
        var (svc, _) = New();
        Assert.False(svc.ToggleEnvironmentTask("opos", "nope").Success);
        Assert.False(svc.RemoveEnvironmentTask("opos", "nope").Success);
    }

    [Fact]
    public void Tasks_sobreviven_round_trip_de_json()
    {
        var (svc, _) = New();
        var id = svc.AddEnvironmentTask("opos", "Persistente").Message;
        svc.ToggleEnvironmentTask("opos", id);
        var json = svc.ExportJson();

        var store2 = new InMemorySettingsStore();
        new ConfigurationService(store2).ImportJson(json);
        var t = store2.Load().FocusEnvironments.Single().Tasks.Single();
        Assert.Equal("Persistente", t.Text);
        Assert.True(t.Done);
    }
}
