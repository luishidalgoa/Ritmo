using System;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

/// <summary>Seguimiento laboral por PROYECTO (#84 V3): agregación, comandos, migración legacy.</summary>
public class WorkTrackingTests
{
    private static WorkLogEntry E(string proj, int y, int m, int d, double h) => new()
    { Id = $"{proj}-{y}{m}{d}-{h}", ProjectId = proj, Date = new DateOnly(y, m, d), Hours = h };

    [Fact]
    public void Horas_del_mes_y_total_por_proyecto()
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
        Assert.Equal(200m, sum.EarningsThisMonth);
        Assert.Equal(62, sum.ProjectedMonthHours, 3);          // 10/5 * 31 días
        Assert.Equal(1240m, sum.ProjectedMonthEarnings);
    }

    [Fact]
    public void Proyecto_sin_horas_da_cero()
    {
        var sum = WorkTracking.Summarize(Array.Empty<WorkLogEntry>(), "x", 30m, new DateOnly(2026, 8, 15));
        Assert.Equal(0, sum.HoursThisMonth);
        Assert.Equal(0m, sum.EarningsThisMonth);
        Assert.Equal(0, sum.ProjectedMonthHours);
    }

    [Fact]
    public void DailyHours_reparte_por_dia_del_mes()
    {
        var log = new[] { E("p1", 2026, 8, 1, 2), E("p1", 2026, 8, 1, 3), E("p1", 2026, 8, 15, 4), E("p1", 2026, 7, 31, 9) };
        var days = WorkTracking.DailyHours(log, "p1", 2026, 8);
        Assert.Equal(31, days.Length);
        Assert.Equal(5, days[0]);     // día 1: 2+3
        Assert.Equal(4, days[14]);    // día 15
        Assert.Equal(0, days[1]);
    }

    [Fact]
    public void CumulativeHours_acumula_dia_a_dia()
    {
        var log = new[] { E("p1", 2026, 8, 1, 2), E("p1", 2026, 8, 3, 4) };
        var cum = WorkTracking.CumulativeHours(log, "p1", 2026, 8);
        Assert.Equal(2, cum[0]);   // día 1
        Assert.Equal(2, cum[1]);   // día 2 (sin horas, mantiene)
        Assert.Equal(6, cum[2]);   // día 3: 2+4
        Assert.Equal(6, cum[^1]);  // fin de mes mantiene el total
    }

    [Fact]
    public void GoalProgress_es_fraccion_y_cero_sin_objetivo()
    {
        Assert.Equal(0.5, WorkTracking.GoalProgress(60, 120), 3);
        Assert.Equal(0, WorkTracking.GoalProgress(60, 0));
    }

    // ---- Comandos de proyecto ----

    private static ConfigurationService New(out InMemorySettingsStore store)
    {
        store = new InMemorySettingsStore();
        return new ConfigurationService(store);
    }

    [Fact]
    public void AddWorkProject_crea_y_devuelve_id()
    {
        var svc = New(out var store);
        var r = svc.AddWorkProject("Cliente A", 25m, 120, "#E53935", "USD");
        Assert.True(r.Success);
        var p = store.Load().WorkProjects.Single();
        Assert.Equal("Cliente A", p.Name);
        Assert.Equal(25m, p.Rate);
        Assert.Equal(120, p.MonthlyGoalHours);
        Assert.Equal("USD", p.CurrencyCode);
        Assert.Equal("$", p.CurrencySymbol);
        Assert.Equal(p.Id, r.Message);
    }

    [Fact]
    public void AddWorkProject_rechaza_nombre_vacio_y_tarifa_negativa()
    {
        var svc = New(out _);
        Assert.False(svc.AddWorkProject("").Success);
        Assert.False(svc.AddWorkProject("X", rate: -1m).Success);
    }

    [Fact]
    public void UpdateWorkProject_cambia_campos_sin_tocar_id()
    {
        var svc = New(out var store);
        var id = svc.AddWorkProject("P", 10m).Message;
        Assert.True(svc.UpdateWorkProject(id, name: "P2", rate: 30m, monthlyGoalHours: 80, archived: true).Success);
        var p = store.Load().WorkProjects.Single();
        Assert.Equal(id, p.Id);
        Assert.Equal("P2", p.Name);
        Assert.Equal(30m, p.Rate);
        Assert.Equal(80, p.MonthlyGoalHours);
        Assert.True(p.Archived);
    }

    [Fact]
    public void RemoveWorkProject_borra_proyecto_y_sus_horas()
    {
        var svc = New(out var store);
        var id = svc.AddWorkProject("P").Message;
        svc.AddWorkHours(id, new DateOnly(2026, 8, 1), 4);
        Assert.True(svc.RemoveWorkProject(id).Success);
        Assert.Empty(store.Load().WorkProjects);
        Assert.Empty(store.Load().WorkLog);   // las horas del proyecto se van con él
    }

    [Fact]
    public void AddWorkHours_exige_proyecto_existente_y_horas_positivas()
    {
        var svc = New(out var store);
        var id = svc.AddWorkProject("P").Message;
        Assert.False(svc.AddWorkHours("noexiste", new DateOnly(2026, 8, 1), 4).Success);
        Assert.False(svc.AddWorkHours(id, new DateOnly(2026, 8, 1), 0).Success);
        Assert.True(svc.AddWorkHours(id, new DateOnly(2026, 8, 1), 4).Success);
        Assert.Single(store.Load().WorkLog);
    }

    [Fact]
    public void UpdateWorkLogEntry_edita_horas_fecha_nota()
    {
        var svc = New(out var store);
        var id = svc.AddWorkProject("P").Message;
        svc.AddWorkHours(id, new DateOnly(2026, 8, 1), 4);
        var e = store.Load().WorkLog.Single();
        Assert.True(svc.UpdateWorkLogEntry(e.Id, hours: 6, note: "diseño").Success);
        var e2 = store.Load().WorkLog.Single();
        Assert.Equal(6, e2.Hours);
        Assert.Equal("diseño", e2.Note);
    }

    [Fact]
    public void El_seguimiento_sobrevive_al_round_trip()
    {
        var svc = New(out _);
        var id = svc.AddWorkProject("Cliente A", 30m, 120, "#43A047", "GBP").Message;
        svc.AddWorkHours(id, new DateOnly(2026, 8, 1), 5, "tarea");
        var json = svc.ExportJson();

        var store2 = new InMemorySettingsStore();
        var svc2 = new ConfigurationService(store2);
        Assert.True(svc2.ImportJson(json).Success);
        var s = store2.Load();
        var p = s.WorkProjects.Single();
        Assert.Equal("Cliente A", p.Name);
        Assert.Equal(30m, p.Rate);
        Assert.Equal("GBP", p.CurrencyCode);
        var e = s.WorkLog.Single();
        Assert.Equal(id, e.ProjectId);
        Assert.Equal(5, e.Hours);
        Assert.Equal("tarea", e.Note);
    }

    [Fact]
    public void Migra_datos_legacy_por_entorno_a_proyectos()
    {
        // Simula un settings.json V1/V2: tarifa/objetivo/horas por ENTORNO.
        const string legacy = """
        {
          "focusEnvironments": [ { "id": "env-1", "name": "Proyecto X" } ],
          "environmentRates": { "env-1": 22 },
          "environmentGoals": { "env-1": 90 },
          "workLog": [ { "id": "w1", "environmentId": "env-1", "date": "2026-08-01", "hours": 5 } ]
        }
        """;
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        Assert.True(svc.ImportJson(legacy).Success);

        var s = store.Load();
        var p = s.WorkProjects.Single();
        Assert.Equal("env-1", p.Id);          // id del entorno se reusa como id de proyecto
        Assert.Equal("Proyecto X", p.Name);
        Assert.Equal(22m, p.Rate);
        Assert.Equal(90, p.MonthlyGoalHours);
        // El WorkLog legacy (environmentId) ahora referencia ese proyecto.
        Assert.Equal("env-1", s.WorkLog.Single().ProjectId);
        Assert.Equal(5, WorkTracking.HoursTotal(s.WorkLog, "env-1"));
    }
}
