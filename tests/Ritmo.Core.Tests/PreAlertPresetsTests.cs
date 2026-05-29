using System;
using System.Linq;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

public class PreAlertPresetsTests
{
    [Theory]
    [InlineData(60, true)]
    [InlineData(10, true)]
    [InlineData(5, true)]
    [InlineData(30, false)]
    [InlineData(1, false)]
    public void IsStandard_reconoce_los_presets(int min, bool expected)
        => Assert.Equal(expected, PreAlertPresets.IsStandard(min));

    [Fact]
    public void Compose_ordena_de_mayor_a_menor_y_deduplica()
    {
        var r = PreAlertPresets.Compose([5, 60, 10, 10]);
        Assert.Equal(new[] { 60, 10, 5 }, r.Select(a => a.MinutesBefore).ToArray());
    }

    [Fact]
    public void Compose_preserva_avisos_no_estandar()
    {
        // El usuario marca 1h y 5min; había un aviso de 30min puesto por la IA.
        var r = PreAlertPresets.Compose([60, 5], preservedNonStandard: [30]);
        Assert.Equal(new[] { 60, 30, 5 }, r.Select(a => a.MinutesBefore).ToArray());
    }

    [Fact]
    public void Compose_ignora_minutos_no_positivos()
    {
        var r = PreAlertPresets.Compose([0, -5, 10]);
        Assert.Equal(new[] { 10 }, r.Select(a => a.MinutesBefore).ToArray());
    }

    [Fact]
    public void NonStandardOf_extrae_solo_los_no_preset()
    {
        var alerts = new[] { new PreAlert(60), new PreAlert(30), new PreAlert(5), new PreAlert(45) };
        var ns = PreAlertPresets.NonStandardOf(alerts);
        Assert.Equal(new[] { 30, 45 }, ns.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Compose_vacio_da_lista_vacia()
        => Assert.Empty(PreAlertPresets.Compose([]));
}
