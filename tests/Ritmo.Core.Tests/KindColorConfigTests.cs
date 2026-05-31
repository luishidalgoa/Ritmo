using Ritmo.Core.Commands;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

/// <summary>Color configurable por categoría de bloque (#45/#83): comando + round-trip.</summary>
public class KindColorConfigTests
{
    private static ConfigurationService New() => new(new InMemorySettingsStore());

    // "concentracion" es una categoría del set neutral de fábrica.
    private const string Cat = "concentracion";

    [Fact]
    public void SetKindColor_en_categoria_inexistente_falla()
        => Assert.False(New().SetKindColor("no-existe", "#AABBCC").Success);

    [Theory]
    [InlineData("#AABBCC", "#AABBCC")]
    [InlineData("aabbcc", "#AABBCC")]   // sin # y en minúsculas -> normalizado
    [InlineData("  #1a2B3c ", "#1A2B3C")]
    public void SetKindColor_normaliza_y_persiste(string input, string expected)
    {
        var svc = New();
        var r = svc.SetKindColor(Cat, input);
        Assert.True(r.Success);
        Assert.Equal(expected, svc.GetSettings().Category(Cat)!.ColorHex);
    }

    [Theory]
    [InlineData("#12345")]   // 5 dígitos
    [InlineData("#GGGGGG")]  // no hex
    [InlineData("rojo")]
    public void SetKindColor_rechaza_colores_invalidos(string bad)
    {
        var r = New().SetKindColor(Cat, bad);
        Assert.False(r.Success);
    }

    [Fact]
    public void SetKindColor_vacio_restablece_el_color()
    {
        var svc = New();
        svc.SetKindColor(Cat, "#102030");
        Assert.Equal("#102030", svc.GetSettings().Category(Cat)!.ColorHex);

        svc.SetKindColor(Cat, "");   // vacío -> color base (gris para categorías no-legacy)
        Assert.NotEqual("#102030", svc.GetSettings().Category(Cat)!.ColorHex);
    }

    [Fact]
    public void Los_colores_sobreviven_al_round_trip()
    {
        var svc = New();
        svc.SetKindColor(Cat, "#FF8800");
        var svc2 = New();
        svc2.ImportJson(svc.ExportJson());
        Assert.Equal("#FF8800", svc2.GetSettings().Category(Cat)!.ColorHex);
    }
}
