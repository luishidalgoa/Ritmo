using Ritmo.Core.Commands;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

/// <summary>
/// Migración al modelo de categorías abiertas (#83): un JSON legacy (sin "categories",
/// con kind="Tecnico" etc.) debe auto-crear las categorías referenciadas, fusionar los
/// colores legacy, garantizar las de sistema y marcar el onboarding si ya hay datos.
/// </summary>
public class CategoryMigrationTests
{
    private static AppSettings Import(string json)
    {
        var svc = new ConfigurationService(new InMemorySettingsStore());
        svc.ImportJson(json);
        return svc.GetSettings();
    }

    // JSON legacy: sin "categories"; sesión kind="Tecnico"; mapeo entorno por "Simulacro";
    // override de color legacy de "Tecnico" en viewConfig.colorsByKind.
    private const string LegacyJson =
        """
        {
          "sessions": [
            { "title": "Bloque", "day": "Monday", "start": "09:00", "durationMinutes": 60, "kind": "Tecnico" }
          ],
          "environmentByKind": { "Simulacro": "env-x" },
          "viewConfig": { "dayStart": "08:00", "dayEnd": "20:00", "colorsByKind": { "Tecnico": "#123456" } }
        }
        """;

    [Fact]
    public void Auto_crea_categoria_para_el_kind_legacy_de_una_sesion()
    {
        var tec = Import(LegacyJson).Category("Tecnico");
        Assert.NotNull(tec);
        Assert.Equal("Técnico", tec!.Name);
        Assert.True(tec.IsFocus);
    }

    [Fact]
    public void El_override_de_color_legacy_gana_sobre_el_color_base()
        => Assert.Equal("#123456", Import(LegacyJson).Category("Tecnico")!.ColorHex);

    [Fact]
    public void Auto_crea_categoria_referenciada_por_el_mapeo_entorno()
    {
        var sim = Import(LegacyJson).Category("Simulacro");
        Assert.NotNull(sim);
        Assert.Equal("Simulacro", sim!.Name);
    }

    [Fact]
    public void Siempre_existen_Otro_y_PorDefinir_como_sistema()
    {
        var s = Import(LegacyJson);
        Assert.True(s.Category(CategoryIds.Other)?.IsSystem);
        Assert.True(s.Category(CategoryIds.Undecided)?.IsSystem);
    }

    [Fact]
    public void Un_id_desconocido_cae_en_categoria_gris_no_focus()
    {
        var json =
            """
            { "sessions": [ { "title": "X", "day": "Monday", "start": "09:00", "durationMinutes": 60, "kind": "mi-cosa" } ] }
            """;
        var c = Import(json).Category("mi-cosa");
        Assert.NotNull(c);
        Assert.False(c!.IsFocus);
        Assert.Equal("#EDEDED", c.ColorHex);
    }

    [Fact]
    public void Con_datos_existentes_marca_el_onboarding_completado()
        => Assert.True(Import(LegacyJson).OnboardingCompleted);

    [Fact]
    public void Las_categorias_migradas_sobreviven_al_round_trip()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.ImportJson(LegacyJson);
        var json2 = svc.ExportJson();

        var svc2 = new ConfigurationService(new InMemorySettingsStore());
        svc2.ImportJson(json2);
        var tec = svc2.GetSettings().Category("Tecnico");
        Assert.Equal("Técnico", tec!.Name);
        Assert.Equal("#123456", tec.ColorHex);
    }
}
