using Ritmo.Core.Commands;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

/// <summary>Color configurable por tipo de bloque (#45): comando + round-trip.</summary>
public class KindColorConfigTests
{
    private static ConfigurationService New() => new(new InMemorySettingsStore());

    [Fact]
    public void Por_defecto_no_hay_colores_personalizados()
        => Assert.Empty(New().GetSettings().ViewConfig.ColorsByKind);

    [Theory]
    [InlineData("#AABBCC", "#AABBCC")]
    [InlineData("aabbcc", "#AABBCC")]   // sin # y en minúsculas -> normalizado
    [InlineData("  #1a2B3c ", "#1A2B3C")]
    public void SetKindColor_normaliza_y_persiste(string input, string expected)
    {
        var svc = New();
        var r = svc.SetKindColor(StudyKind.Tecnico, input);
        Assert.True(r.Success);
        Assert.Equal(expected, svc.GetSettings().ViewConfig.ColorsByKind[StudyKind.Tecnico]);
    }

    [Theory]
    [InlineData("#12345")]   // 5 dígitos
    [InlineData("#GGGGGG")]  // no hex
    [InlineData("rojo")]
    public void SetKindColor_rechaza_colores_invalidos(string bad)
    {
        var r = New().SetKindColor(StudyKind.Tests, bad);
        Assert.False(r.Success);
    }

    [Fact]
    public void SetKindColor_vacio_quita_el_override()
    {
        var svc = New();
        svc.SetKindColor(StudyKind.Ingles, "#102030");
        Assert.True(svc.GetSettings().ViewConfig.ColorsByKind.ContainsKey(StudyKind.Ingles));

        svc.SetKindColor(StudyKind.Ingles, "");
        Assert.False(svc.GetSettings().ViewConfig.ColorsByKind.ContainsKey(StudyKind.Ingles));
    }

    [Fact]
    public void Los_colores_sobreviven_al_round_trip()
    {
        var svc = New();
        svc.SetKindColor(StudyKind.Simulacro, "#FF8800");
        var svc2 = New();
        svc2.ImportJson(svc.ExportJson());
        Assert.Equal("#FF8800", svc2.GetSettings().ViewConfig.ColorsByKind[StudyKind.Simulacro]);
    }
}
