using System;
using System.Collections.Generic;
using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

public class OneOffPlannerTests
{
    private static OneOffSession One(DateOnly date, int h, double durH = 1, string title = "Extra") => new()
    {
        Id = Guid.NewGuid().ToString("N")[..8], Date = date, Title = title,
        Start = new TimeOnly(h, 0), Duration = TimeSpan.FromHours(durH), CategoryId = "Ingles"
    };

    private static readonly DateOnly Today = new(2026, 5, 30);

    [Fact]
    public void ActiveAt_devuelve_la_que_cubre_ahora()
    {
        var list = new List<OneOffSession> { One(Today, 15, 2) };   // 15:00–17:00
        var now = new DateTime(2026, 5, 30, 16, 0, 0);
        Assert.NotNull(OneOffPlanner.ActiveAt(list, now));
        // fuera de la franja
        Assert.Null(OneOffPlanner.ActiveAt(list, new DateTime(2026, 5, 30, 17, 30, 0)));
        // otro día
        Assert.Null(OneOffPlanner.ActiveAt(list, new DateTime(2026, 5, 31, 16, 0, 0)));
    }

    [Fact]
    public void ActiveAt_borde_inicio_incluido_fin_excluido()
    {
        var list = new List<OneOffSession> { One(Today, 15, 1) };   // 15:00–16:00
        Assert.NotNull(OneOffPlanner.ActiveAt(list, new DateTime(2026, 5, 30, 15, 0, 0)));   // inicio incluido
        Assert.Null(OneOffPlanner.ActiveAt(list, new DateTime(2026, 5, 30, 16, 0, 0)));      // fin excluido
    }

    [Fact]
    public void ActiveAt_funciona_aunque_cruce_medianoche()
    {
        // 23:17 + 60 min llega a 00:17: no debe "envolver" y romper la comparación.
        var list = new List<OneOffSession> { One(Today, 23, 1) };   // 23:00–24:00
        Assert.NotNull(OneOffPlanner.ActiveAt(list, new DateTime(2026, 5, 30, 23, 33, 0)));
    }

    [Fact]
    public void NextToday_la_proxima_que_empieza_despues()
    {
        var list = new List<OneOffSession> { One(Today, 18, 1, "Tarde"), One(Today, 10, 1, "Maniana") };
        var now = new DateTime(2026, 5, 30, 12, 0, 0);
        var next = OneOffPlanner.NextToday(list, now);
        Assert.NotNull(next);
        Assert.Equal("Tarde", next!.Title);   // la de las 18:00 (la de las 10:00 ya pasó)
    }
}
