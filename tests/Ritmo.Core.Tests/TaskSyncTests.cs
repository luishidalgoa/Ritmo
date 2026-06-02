using System.Linq;
using Ritmo.Core.Sync;

namespace Ritmo.Core.Tests;

public class TaskSyncTests
{
    private static SyncLocalTask L(string id, string? ext, string text, bool done = false, string? upd = null)
        => new(id, ext, text, done, upd);
    private static SyncRemoteTask R(string ext, string title, bool done = false, string upd = "T1")
        => new(ext, title, done, upd);

    [Fact]
    public void Local_sin_external_se_empuja_como_nueva()
    {
        var plan = TaskSync.Plan(new[] { L("a", null, "Comprar") }, System.Array.Empty<SyncRemoteTask>());
        Assert.Single(plan.PushNew);
        Assert.Equal("a", plan.PushNew[0].LocalId);
        Assert.Empty(plan.PullNew);
        Assert.Empty(plan.DeleteLocal);
    }

    [Fact]
    public void Remota_sin_local_se_crea_en_local()
    {
        var plan = TaskSync.Plan(System.Array.Empty<SyncLocalTask>(), new[] { R("g1", "Llamar") });
        Assert.Single(plan.PullNew);
        Assert.Equal("g1", plan.PullNew[0].ExternalId);
    }

    [Fact]
    public void Google_cambio_gana_google_pull()
    {
        // Mismo par; el updated remoto difiere del guardado → pull.
        var plan = TaskSync.Plan(
            new[] { L("a", "g1", "viejo", false, "T0") },
            new[] { R("g1", "nuevo", true, "T1") });
        Assert.Single(plan.PullUpdate);
        Assert.Equal("a", plan.PullUpdate[0].LocalId);
        Assert.Equal("nuevo", plan.PullUpdate[0].Remote.Title);
        Assert.Empty(plan.PushUpdate);
    }

    [Fact]
    public void Google_igual_local_difiere_gana_local_push()
    {
        // updated remoto == guardado (Google no cambió) y el texto/estado local difiere → push.
        var plan = TaskSync.Plan(
            new[] { L("a", "g1", "editado en Ritmo", true, "T1") },
            new[] { R("g1", "original", false, "T1") });
        Assert.Single(plan.PushUpdate);
        Assert.Equal("a", plan.PushUpdate[0].LocalId);
        Assert.Empty(plan.PullUpdate);
    }

    [Fact]
    public void Sin_cambios_no_hace_nada()
    {
        var plan = TaskSync.Plan(
            new[] { L("a", "g1", "igual", false, "T1") },
            new[] { R("g1", "igual", false, "T1") });
        Assert.Empty(plan.PushNew);
        Assert.Empty(plan.PushUpdate);
        Assert.Empty(plan.PullUpdate);
        Assert.Empty(plan.PullNew);
        Assert.Empty(plan.DeleteLocal);
    }

    [Fact]
    public void Borrada_en_google_se_borra_en_local()
    {
        var plan = TaskSync.Plan(
            new[] { L("a", "g1", "huérfana", false, "T1") },
            System.Array.Empty<SyncRemoteTask>());
        Assert.Single(plan.DeleteLocal);
        Assert.Equal("a", plan.DeleteLocal[0]);
    }

    [Fact]
    public void Mezcla_completa()
    {
        var local = new[]
        {
            L("a", null, "nueva local"),            // → push new
            L("b", "g1", "local difiere", true, "T1"), // Google igual → push update
            L("c", "g2", "local viejo", false, "T0"),  // Google cambió → pull update
            L("d", "g3", "borrada en google", false, "T1") // ya no en remoto → delete local
        };
        var remote = new[]
        {
            R("g1", "local difiere", false, "T1"),  // mismo updated; difiere el done
            R("g2", "google nuevo", true, "T9"),     // updated distinto → pull
            R("g4", "remota nueva", false, "T1")     // sin local → pull new
        };
        var plan = TaskSync.Plan(local, remote);
        Assert.Equal("a", Assert.Single(plan.PushNew).LocalId);
        Assert.Equal("b", Assert.Single(plan.PushUpdate).LocalId);
        Assert.Equal("c", Assert.Single(plan.PullUpdate).LocalId);
        Assert.Equal("g4", Assert.Single(plan.PullNew).ExternalId);
        Assert.Equal("d", Assert.Single(plan.DeleteLocal));
    }
}
