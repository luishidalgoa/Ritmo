using Ritmo.Core.Commands;

namespace Ritmo.Core.Tests;

/// <summary>
/// Aviso previo por defecto (#48): el valor con que se pre-rellena una sesión nueva.
/// Comando + validación + persistencia (round-trip DTO).
/// </summary>
public class DefaultPreAlertConfigTests
{
    private static ConfigurationService New() => new(new InMemorySettingsStore());

    [Fact]
    public void Por_defecto_el_aviso_previo_es_10()
    {
        var svc = New();
        Assert.Equal(10, svc.GetSettings().ViewConfig.DefaultPreAlertMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(60)]
    [InlineData(1440)]
    public void SetDefaultPreAlert_acepta_el_rango_valido(int minutes)
    {
        var svc = New();
        var r = svc.SetDefaultPreAlert(minutes);
        Assert.True(r.Success);
        Assert.Equal(minutes, svc.GetSettings().ViewConfig.DefaultPreAlertMinutes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1441)]
    public void SetDefaultPreAlert_rechaza_fuera_de_rango_sin_cambiar_el_valor(int minutes)
    {
        var svc = New();
        var r = svc.SetDefaultPreAlert(minutes);
        Assert.False(r.Success);
        Assert.Equal(10, svc.GetSettings().ViewConfig.DefaultPreAlertMinutes);   // intacto
    }

    [Fact]
    public void El_aviso_previo_por_defecto_sobrevive_al_round_trip()
    {
        var svc = New();
        svc.SetDefaultPreAlert(30);
        var json = svc.ExportJson();

        var svc2 = New();
        var imported = svc2.ImportJson(json);
        Assert.True(imported.Success);
        Assert.Equal(30, svc2.GetSettings().ViewConfig.DefaultPreAlertMinutes);
    }
}
