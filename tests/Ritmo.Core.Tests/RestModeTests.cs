using System;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

/// <summary>Modo descanso (#135): manual + periodos programados; pausa los avisos en esas fechas.</summary>
public class RestModeTests
{
    private static ConfigurationService New(out InMemorySettingsStore store)
    {
        store = new InMemorySettingsStore();
        return new ConfigurationService(store);
    }

    [Fact]
    public void Por_defecto_no_hay_descanso()
    {
        var s = AppSettings.Default;
        Assert.False(s.RestActive);
        Assert.Empty(s.RestPeriods);
        Assert.False(s.IsRestingOn(new DateOnly(2026, 7, 15)));
    }

    [Fact]
    public void Descanso_manual_activo_descansa_cualquier_fecha()
    {
        var svc = New(out var store);
        Assert.True(svc.SetRestActive(true).Success);
        var s = store.Load();
        Assert.True(s.IsRestingOn(new DateOnly(2026, 1, 1)));
        Assert.True(s.IsRestingOn(new DateOnly(2030, 12, 31)));
    }

    [Fact]
    public void Un_periodo_cubre_sus_fechas_inclusive_y_no_las_de_fuera()
    {
        var svc = New(out var store);
        Assert.True(svc.AddRestPeriod(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15), "Vacaciones").Success);
        var s = store.Load();
        Assert.True(s.IsRestingOn(new DateOnly(2026, 8, 1)));    // inicio inclusive
        Assert.True(s.IsRestingOn(new DateOnly(2026, 8, 15)));   // fin inclusive
        Assert.True(s.IsRestingOn(new DateOnly(2026, 8, 10)));
        Assert.False(s.IsRestingOn(new DateOnly(2026, 7, 31)));  // antes
        Assert.False(s.IsRestingOn(new DateOnly(2026, 8, 16)));  // después
    }

    [Fact]
    public void AddRestPeriod_rechaza_fin_antes_de_inicio()
    {
        var svc = New(out var store);
        Assert.False(svc.AddRestPeriod(new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 1)).Success);
        Assert.Empty(store.Load().RestPeriods);
    }

    [Fact]
    public void RemoveRestPeriod_quita_por_id()
    {
        var svc = New(out var store);
        svc.AddRestPeriod(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15), "V");
        var id = store.Load().RestPeriods.Single().Id;
        Assert.True(svc.RemoveRestPeriod(id).Success);
        Assert.Empty(store.Load().RestPeriods);
    }

    [Fact]
    public void El_descanso_sobrevive_al_round_trip()
    {
        var svc = New(out _);
        svc.SetRestActive(true);
        svc.AddRestPeriod(new DateOnly(2026, 12, 24), new DateOnly(2027, 1, 6), "Navidad");
        var json = svc.ExportJson();

        var svc2 = New(out var store2);
        Assert.True(svc2.ImportJson(json).Success);
        var s = store2.Load();
        Assert.True(s.RestActive);
        var p = s.RestPeriods.Single();
        Assert.Equal(new DateOnly(2026, 12, 24), p.From);
        Assert.Equal(new DateOnly(2027, 1, 6), p.To);
        Assert.Equal("Navidad", p.Label);
    }
}
