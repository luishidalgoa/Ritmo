using Ritmo.Core.Commands;

namespace Ritmo.Core.Tests;

/// <summary>Granularidad de la rejilla (#61): comando + persistencia (round-trip DTO).</summary>
public class GranularityConfigTests
{
    private static ConfigurationService New() => new(new InMemorySettingsStore());

    [Fact]
    public void Por_defecto_la_granularidad_es_60()
    {
        var svc = New();
        Assert.Equal(60, svc.GetSettings().ViewConfig.GranularityMinutes);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(30)]
    [InlineData(15)]
    public void SetGranularity_acepta_60_30_15(int minutes)
    {
        var svc = New();
        var r = svc.SetGranularity(minutes);
        Assert.True(r.Success);
        Assert.Equal(minutes, svc.GetSettings().ViewConfig.GranularityMinutes);
    }

    [Theory]
    [InlineData(45)]
    [InlineData(0)]
    [InlineData(10)]
    public void SetGranularity_rechaza_valores_no_admitidos(int minutes)
    {
        var svc = New();
        var r = svc.SetGranularity(minutes);
        Assert.False(r.Success);
        // No cambia el valor previo (sigue el por defecto).
        Assert.Equal(60, svc.GetSettings().ViewConfig.GranularityMinutes);
    }

    [Fact]
    public void La_granularidad_sobrevive_al_round_trip_de_persistencia()
    {
        var svc = New();
        svc.SetGranularity(15);
        var json = svc.ExportJson();

        var svc2 = New();
        var imported = svc2.ImportJson(json);
        Assert.True(imported.Success);
        Assert.Equal(15, svc2.GetSettings().ViewConfig.GranularityMinutes);
    }
}
