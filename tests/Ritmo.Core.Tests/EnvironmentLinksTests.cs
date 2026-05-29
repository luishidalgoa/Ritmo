using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Focus;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

public class EnvironmentLinksTests
{
    private static (ConfigurationService svc, ISettingsStore store) New()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.UpsertEnvironment(new FocusEnvironment { Id = "opos", Name = "Oposiciones" });
        return (svc, store);
    }

    [Fact]
    public void AddEnvironmentLink_anade_al_entorno()
    {
        var (svc, store) = New();
        Assert.True(svc.AddEnvironmentLink("opos", "Campus", "https://campus.zbrain.es").Success);
        Assert.True(svc.AddEnvironmentLink("opos", "BOE", "https://boe.es").Success);
        var env = store.Load().FocusEnvironments.Single();
        Assert.Equal(2, env.Links.Count);
        Assert.Equal("Campus", env.Links[0].Title);
    }

    [Fact]
    public void AddEnvironmentLink_valida()
    {
        var (svc, _) = New();
        Assert.False(svc.AddEnvironmentLink("opos", "", "https://x").Success);
        Assert.False(svc.AddEnvironmentLink("opos", "X", " ").Success);
        Assert.False(svc.AddEnvironmentLink("nope", "X", "https://x").Success);
    }

    [Fact]
    public void RemoveEnvironmentLink_borra_por_indice()
    {
        var (svc, store) = New();
        svc.AddEnvironmentLink("opos", "Campus", "https://campus.zbrain.es");
        svc.AddEnvironmentLink("opos", "BOE", "https://boe.es");
        Assert.True(svc.RemoveEnvironmentLink("opos", 0).Success);
        var env = store.Load().FocusEnvironments.Single();
        Assert.Single(env.Links);
        Assert.Equal("BOE", env.Links[0].Title);
    }

    [Fact]
    public void RemoveEnvironmentLink_fuera_de_rango_falla()
    {
        var (svc, _) = New();
        Assert.False(svc.RemoveEnvironmentLink("opos", 0).Success);
    }

    [Fact]
    public void Links_sobreviven_round_trip_de_json()
    {
        var (svc, _) = New();
        svc.AddEnvironmentLink("opos", "Campus", "https://campus.zbrain.es");
        var json = svc.ExportJson();

        var store2 = new InMemorySettingsStore();
        new ConfigurationService(store2).ImportJson(json);
        var env = store2.Load().FocusEnvironments.Single();
        Assert.Single(env.Links);
        Assert.Equal("https://campus.zbrain.es", env.Links[0].Url);
    }
}
