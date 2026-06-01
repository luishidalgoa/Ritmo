using System;
using Ritmo.Core.Commands;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

public class SessionCleanupTests
{
    private static (ConfigurationService svc, ISettingsStore store) New()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.AddPhase("F1", new DateOnly(2026, 1, 1), null);
        return (svc, store);
    }

    private static StudySession S(string t, DayOfWeek d, int hour = 9) =>
        new() { Title = t, Day = d, Start = new TimeOnly(hour, 0), Duration = TimeSpan.FromHours(1), CategoryId = "Trabajo" };

    [Fact]
    public void Borrar_sesion_poda_su_excepcion_huerfana()
    {
        var (svc, store) = New();
        var sesion = S("Heladería", DayOfWeek.Monday);
        svc.AddSession("F1", sesion);
        svc.AddSessionException(SessionKey.For(sesion), new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5));
        Assert.Single(store.Load().SessionExceptions);

        // Borra la sesión (lista vacía) y limpia huérfanos.
        svc.ReplaceSessions("F1", Array.Empty<StudySession>());
        svc.PruneOrphanSessionData();

        Assert.Empty(store.Load().SessionExceptions);
    }

    [Fact]
    public void RemoveSession_tambien_poda()
    {
        var (svc, store) = New();
        var sesion = S("Heladería", DayOfWeek.Monday);
        svc.AddSession("F1", sesion);
        svc.AddSessionException(SessionKey.For(sesion), new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5));

        svc.RemoveSession("F1", 0);

        Assert.Empty(store.Load().SessionExceptions);
    }

    [Fact]
    public void Conserva_excepcion_si_otra_sesion_comparte_la_clave()
    {
        var (svc, store) = New();
        // Misma clave (mismo título/categoría/inicio/duración) en dos días distintos.
        var lunes = S("Heladería", DayOfWeek.Monday);
        var martes = S("Heladería", DayOfWeek.Tuesday);
        svc.AddSession("F1", lunes);
        svc.AddSession("F1", martes);
        svc.AddSessionException(SessionKey.For(lunes), new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5));

        // Borra solo el lunes: el martes sigue vivo con la MISMA clave → la excepción se conserva.
        svc.ReplaceSessions("F1", new[] { martes });
        svc.PruneOrphanSessionData();

        Assert.Single(store.Load().SessionExceptions);
    }

    [Fact]
    public void No_poda_al_redimensionar_si_se_conserva_la_clave()
    {
        var (svc, store) = New();
        var sesion = S("Heladería", DayOfWeek.Monday);
        svc.AddSession("F1", sesion);
        svc.AddSessionException(SessionKey.For(sesion), new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5));

        // No borramos nada: PruneOrphanSessionData no debe tocar la excepción viva.
        var r = svc.PruneOrphanSessionData();

        Assert.True(r.Success);
        Assert.Single(store.Load().SessionExceptions);
    }

    [Fact]
    public void Prune_es_idempotente_sin_huerfanos()
    {
        var (svc, _) = New();
        svc.AddSession("F1", S("X", DayOfWeek.Monday));
        var r = svc.PruneOrphanSessionData();
        Assert.True(r.Success);
        Assert.Contains("Sin huérfanos", r.Message);
    }

    [Fact]
    public void Helper_puro_poda_por_clave_inexistente()
    {
        var viva = S("Viva", DayOfWeek.Monday);
        var muerta = S("Muerta", DayOfWeek.Monday);
        var exc = new[]
        {
            new SessionException { Id = "a", SessionKey = SessionKey.For(viva), From = new DateOnly(2026,1,1), To = new DateOnly(2026,1,1) },
            new SessionException { Id = "b", SessionKey = SessionKey.For(muerta), From = new DateOnly(2026,1,1), To = new DateOnly(2026,1,1) },
        };
        var pruned = SessionCleanup.PruneOrphanExceptions(new[] { viva }, exc);
        Assert.Single(pruned);
        Assert.Equal("a", pruned[0].Id);
    }
}
