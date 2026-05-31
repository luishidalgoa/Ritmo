using System;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

/// <summary>Seguimiento laboral (#84): horas/ganado del mes, proyección, y comandos.</summary>
public class WorkTrackingTests
{
    private static WorkLogEntry E(string env, int y, int m, int d, double h) => new()
    { Id = $"{env}-{y}{m}{d}-{h}", EnvironmentId = env, Date = new DateOnly(y, m, d), Hours = h };

    [Fact]
    public void Horas_del_mes_y_total_por_entorno()
    {
        var log = new[] { E("p1", 2026, 8, 1, 3), E("p1", 2026, 8, 2, 5), E("p1", 2026, 7, 30, 4), E("p2", 2026, 8, 1, 2) };
        Assert.Equal(8, WorkTracking.HoursInMonth(log, "p1", 2026, 8));
        Assert.Equal(12, WorkTracking.HoursTotal(log, "p1"));
        Assert.Equal(2, WorkTracking.HoursInMonth(log, "p2", 2026, 8));
    }

    [Fact]
    public void Resumen_calcula_ganado_y_proyeccion()
    {
        var log = new[] { E("p1", 2026, 8, 1, 4), E("p1", 2026, 8, 5, 6) };   // 10 h hasta el día 5
        var sum = WorkTracking.Summarize(log, "p1", 20m, new DateOnly(2026, 8, 5));
        Assert.Equal(10, sum.HoursThisMonth);
        Assert.Equal(200m, sum.EarningsThisMonth);              // 10 * 20
        Assert.Equal(62, sum.ProjectedMonthHours, 3);          // 10/5 * 31 días
        Assert.Equal(1240m, sum.ProjectedMonthEarnings);
    }

    [Fact]
    public void Entorno_sin_horas_da_cero()
    {
        var sum = WorkTracking.Summarize(Array.Empty<WorkLogEntry>(), "x", 30m, new DateOnly(2026, 8, 15));
        Assert.Equal(0, sum.HoursThisMonth);
        Assert.Equal(0m, sum.EarningsThisMonth);
        Assert.Equal(0, sum.ProjectedMonthHours);
    }

    private static ConfigurationService NewWithEnv(out InMemorySettingsStore store, string envId = "p1")
    {
        store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.UpsertEnvironment(new FocusEnvironment { Id = envId, Name = "Proyecto" });
        return svc;
    }

    [Fact]
    public void SetEnvironmentRate_guarda_y_quita_con_cero()
    {
        var svc = NewWithEnv(out var store);
        Assert.True(svc.SetEnvironmentRate("p1", 25m).Success);
        Assert.Equal(25m, store.Load().EnvironmentRates["p1"]);
        Assert.True(svc.SetEnvironmentRate("p1", 0m).Success);
        Assert.False(store.Load().EnvironmentRates.ContainsKey("p1"));
    }

    [Fact]
    public void SetEnvironmentRate_rechaza_negativa_y_entorno_inexistente()
    {
        var svc = NewWithEnv(out _);
        Assert.False(svc.SetEnvironmentRate("p1", -1m).Success);
        Assert.False(svc.SetEnvironmentRate("noexiste", 10m).Success);
    }

    [Fact]
    public void AddWorkHours_anota_y_RemoveWorkLogEntry_quita()
    {
        var svc = NewWithEnv(out var store);
        Assert.True(svc.AddWorkHours("p1", new DateOnly(2026, 8, 1), 4).Success);
        var entry = store.Load().WorkLog.Single();
        Assert.Equal(4, entry.Hours);
        Assert.True(svc.RemoveWorkLogEntry(entry.Id).Success);
        Assert.Empty(store.Load().WorkLog);
    }

    [Fact]
    public void AddWorkHours_rechaza_cero()
    {
        var svc = NewWithEnv(out var store);
        Assert.False(svc.AddWorkHours("p1", new DateOnly(2026, 8, 1), 0).Success);
        Assert.Empty(store.Load().WorkLog);
    }

    [Fact]
    public void El_seguimiento_sobrevive_al_round_trip()
    {
        var svc = NewWithEnv(out _);
        svc.SetEnvironmentRate("p1", 30m);
        svc.AddWorkHours("p1", new DateOnly(2026, 8, 1), 5);
        var json = svc.ExportJson();

        var store2 = new InMemorySettingsStore();
        var svc2 = new ConfigurationService(store2);
        Assert.True(svc2.ImportJson(json).Success);
        var s = store2.Load();
        Assert.Equal(30m, s.EnvironmentRates["p1"]);
        Assert.Equal(5, s.WorkLog.Single().Hours);
    }
}
