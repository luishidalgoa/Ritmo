using System;
using System.Collections.Generic;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

public class SessionProfileTests
{
    private static FocusEnvironment Env(IReadOnlyList<SessionAppProfile>? profiles = null) => new()
    {
        Id = "env-1", Name = "Estudio",
        Links =
        [
            new ShortcutLink { Title = "BOE", Url = "https://boe.es" },
            new ShortcutLink { Title = "Campus", Url = "https://campus.edu" },
        ],
        AppsToOpen = ["WINWORD", "Code"],
        SessionProfiles = profiles ?? []
    };

    [Fact]
    public void ResolveOpen_sin_perfil_abre_todo()
    {
        var env = Env();
        var (links, apps) = env.ResolveOpen("Legislación");
        Assert.Equal(2, links.Count);
        Assert.Equal(2, apps.Count);
    }

    [Fact]
    public void ResolveOpen_sin_titulo_abre_todo()
    {
        var env = Env();
        var (links, apps) = env.ResolveOpen(null);
        Assert.Equal(2, links.Count);
        Assert.Equal(2, apps.Count);
    }

    [Fact]
    public void ResolveOpen_con_perfil_devuelve_solo_el_subconjunto()
    {
        var env = Env([new SessionAppProfile
        {
            SessionTitle = "Legislación",
            EnabledLinks = ["https://boe.es"],
            EnabledApps = ["WINWORD"]
        }]);

        var (links, apps) = env.ResolveOpen("legislación");   // case-insensitive
        Assert.Single(links);
        Assert.Equal("https://boe.es", links[0].Url);
        Assert.Single(apps);
        Assert.Equal("WINWORD", apps[0]);

        // Otro título sin perfil sigue abriendo todo.
        Assert.Equal(2, env.ResolveOpen("Técnico").Links.Count);
    }

    [Fact]
    public void ResolveOpen_intersecta_con_los_actuales()
    {
        // El perfil referencia un enlace/app que ya no existe en el entorno -> se ignora.
        var env = Env([new SessionAppProfile
        {
            SessionTitle = "Tests",
            EnabledLinks = ["https://boe.es", "https://ya-no-existe.com"],
            EnabledApps = ["WINWORD", "AppBorrada"]
        }]);
        var (links, apps) = env.ResolveOpen("Tests");
        Assert.Single(links);
        Assert.Single(apps);
    }

    [Fact]
    public void DistinctTitles_normaliza_y_deduplica()
    {
        var sessions = new[]
        {
            new StudySession { Title = "Técnico", Day = DayOfWeek.Monday, Start = new TimeOnly(9, 0), Duration = TimeSpan.FromHours(2) },
            new StudySession { Title = " Técnico ", Day = DayOfWeek.Tuesday, Start = new TimeOnly(9, 0), Duration = TimeSpan.FromHours(2) },
            new StudySession { Title = "Tests del tema", Day = DayOfWeek.Monday, Start = new TimeOnly(16, 0), Duration = TimeSpan.FromHours(2) },
        };
        var titles = SessionGrouping.DistinctTitles(sessions);
        Assert.Equal(2, titles.Count);
        Assert.Contains("Técnico", titles);
        Assert.Contains("Tests del tema", titles);
    }

    [Fact]
    public void Comandos_Set_y_Clear_perfil()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        var env = Env();
        svc.UpsertEnvironment(env);

        Assert.True(svc.SetSessionProfile("env-1", "Legislación", ["https://boe.es"], ["WINWORD"]).Success);
        var saved = store.Load().FocusEnvironments.Single();
        var p = Assert.Single(saved.SessionProfiles);
        Assert.Equal("Legislación", p.SessionTitle);
        Assert.Single(p.EnabledLinks);

        // Reemplaza (no duplica) el del mismo título.
        Assert.True(svc.SetSessionProfile("env-1", "legislación", [], []).Success);
        Assert.Single(store.Load().FocusEnvironments.Single().SessionProfiles);

        Assert.True(svc.ClearSessionProfile("env-1", "Legislación").Success);
        Assert.Empty(store.Load().FocusEnvironments.Single().SessionProfiles);
    }

    [Fact]
    public void Mapear_y_desmapear_tipo_a_entorno()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.UpsertEnvironment(Env());

        Assert.True(svc.MapEnvironmentToKind("Tecnico", "env-1").Success);
        Assert.Equal("env-1", store.Load().EnvironmentByKind["Tecnico"]);

        Assert.True(svc.ClearEnvironmentKind("Tecnico").Success);
        Assert.False(store.Load().EnvironmentByKind.ContainsKey("Tecnico"));
    }
}
