using System.Linq;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

/// <summary>Categorías de bloque (#83): slug, fallback y helpers de AppSettings.</summary>
public class BlockCategoryTests
{
    [Fact]
    public void Slug_normaliza_acentos_y_espacios()
        => Assert.Equal("lectura-critica", CategorySlug.From("Lectura crítica"));

    [Fact]
    public void Slug_resuelve_colisiones_con_sufijo()
        => Assert.Equal("foco-2", CategorySlug.From("Foco", new[] { "foco" }));

    [Fact]
    public void Slug_vacio_cae_en_categoria()
        => Assert.Equal("categoria", CategorySlug.From("   "));

    [Fact]
    public void Default_neutral_incluye_sistema_y_focus()
    {
        var s = new AppSettings();
        Assert.NotNull(s.Category(CategoryIds.Other));
        Assert.NotNull(s.Category(CategoryIds.Undecided));
        Assert.Contains("concentracion", s.FocusCategoryIds());
        Assert.DoesNotContain("descanso", s.FocusCategoryIds());
    }

    [Fact]
    public void CategoryOrFallback_de_id_desconocido_es_Otro()
        => Assert.Equal(CategoryIds.Other, new AppSettings().CategoryOrFallback("no-existe").Id);

    [Fact]
    public void CategoryName_resuelve_conocido_y_cae_en_Otro()
    {
        var s = new AppSettings();
        Assert.Equal("Concentración", s.CategoryName("concentracion"));
        Assert.Equal("Otro", s.CategoryName("zzz-desconocido"));
    }

    [Fact]
    public void IsFocusCategory_segun_la_definicion()
    {
        var s = new AppSettings();
        Assert.True(s.IsFocusCategory("concentracion"));
        Assert.False(s.IsFocusCategory("descanso"));
        Assert.False(s.IsFocusCategory("no-existe"));
    }
}
