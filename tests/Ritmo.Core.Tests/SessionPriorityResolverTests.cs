using System;
using System.Collections.Generic;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

public class SessionPriorityResolverTests
{
    private static StudySession S(string title, int hour, int durHours = 2) => new()
    {
        Title = title, Day = DayOfWeek.Tuesday,
        Start = new TimeOnly(hour, 0), Duration = TimeSpan.FromHours(durHours),
        CategoryId = "x"
    };

    private static (StudySession, string)[] WithKeys(params (StudySession s, string k)[] items)
        => items.Select(i => (i.s, i.k)).ToArray();

    [Fact]
    public void Sin_marcadas_todo_es_normal_aunque_solapen()
    {
        var a = S("A", 16); var b = S("B", 16);   // 16–18 ambas, chocan
        var states = SessionPriorityResolver.Resolve(
            WithKeys((a, "a"), (b, "b")), new HashSet<string>());

        Assert.Equal(ConflictState.Normal, states["a"]);
        Assert.Equal(ConflictState.Normal, states["b"]);
    }

    [Fact]
    public void Una_marcada_la_otra_recede()
    {
        var a = S("A", 16); var b = S("B", 16);
        var states = SessionPriorityResolver.Resolve(
            WithKeys((a, "a"), (b, "b")), new HashSet<string> { "a" });

        Assert.Equal(ConflictState.Priority, states["a"]);
        Assert.Equal(ConflictState.Receded, states["b"]);
    }

    [Fact]
    public void Ambas_marcadas_ambas_prioritarias()
    {
        var a = S("A", 16); var b = S("B", 16);
        var states = SessionPriorityResolver.Resolve(
            WithKeys((a, "a"), (b, "b")), new HashSet<string> { "a", "b" });

        Assert.Equal(ConflictState.Priority, states["a"]);
        Assert.Equal(ConflictState.Priority, states["b"]);
    }

    [Fact]
    public void Tres_solapan_dos_prioritarias_una_recede()
    {
        var a = S("A", 16); var b = S("B", 16); var c = S("C", 16);
        var states = SessionPriorityResolver.Resolve(
            WithKeys((a, "a"), (b, "b"), (c, "c")), new HashSet<string> { "a", "b" });

        Assert.Equal(ConflictState.Priority, states["a"]);
        Assert.Equal(ConflictState.Priority, states["b"]);
        Assert.Equal(ConflictState.Receded, states["c"]);
    }

    [Fact]
    public void Sin_solape_la_marca_no_afecta()
    {
        var a = S("A", 9); var b = S("B", 16);   // no chocan
        var states = SessionPriorityResolver.Resolve(
            WithKeys((a, "a"), (b, "b")), new HashSet<string> { "a" });

        // Una marcada pero sola en su franja: sigue normal (no hay a quién destacar frente a quién).
        Assert.Equal(ConflictState.Normal, states["a"]);
        Assert.Equal(ConflictState.Normal, states["b"]);
    }

    [Fact]
    public void Dos_grupos_distintos_no_se_contaminan()
    {
        // Grupo 1 (mañana): a+b chocan, a prioritaria. Grupo 2 (tarde): c+d chocan, sin marcar.
        var a = S("A", 9); var b = S("B", 9);
        var c = S("C", 16); var d = S("D", 16);
        var states = SessionPriorityResolver.Resolve(
            WithKeys((a, "a"), (b, "b"), (c, "c"), (d, "d")), new HashSet<string> { "a" });

        Assert.Equal(ConflictState.Priority, states["a"]);
        Assert.Equal(ConflictState.Receded, states["b"]);
        Assert.Equal(ConflictState.Normal, states["c"]);   // otro grupo: NO recede
        Assert.Equal(ConflictState.Normal, states["d"]);
    }

    [Fact]
    public void Vacio_devuelve_vacio()
        => Assert.Empty(SessionPriorityResolver.Resolve(Array.Empty<(StudySession, string)>(), new HashSet<string>()));

    // ---------- ConfigurationService ----------

    [Fact]
    public void SetSessionPriority_marca_y_desmarca()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        const string key = "2|A|x|16:00|02:00:00|False";

        Assert.True(svc.SetSessionPriority(key, priority: true).Success);
        Assert.Single(store.Load().SessionPriorities);

        // Idempotente: marcar de nuevo no duplica.
        svc.SetSessionPriority(key, priority: true);
        Assert.Single(store.Load().SessionPriorities);

        Assert.True(svc.SetSessionPriority(key, priority: false).Success);
        Assert.Empty(store.Load().SessionPriorities);
    }

    [Fact]
    public void SetSessionPriority_clave_vacia_falla()
    {
        var svc = new ConfigurationService(new InMemorySettingsStore());
        Assert.False(svc.SetSessionPriority("  ", priority: true).Success);
    }

    [Fact]
    public void Varias_prioritarias_coexisten()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.SetSessionPriority("k1", priority: true);
        svc.SetSessionPriority("k2", priority: true);
        Assert.Equal(2, store.Load().SessionPriorities.Count);
    }

    [Fact]
    public void Prioridad_de_sesion_sobrevive_export_import()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.SetSessionPriority("k|sobrevive", priority: true);

        var json = svc.ExportJson();
        var other = new ConfigurationService(new InMemorySettingsStore());
        Assert.True(other.ImportJson(json).Success);
        Assert.Single(other.GetSettings().SessionPriorities);
    }
}
