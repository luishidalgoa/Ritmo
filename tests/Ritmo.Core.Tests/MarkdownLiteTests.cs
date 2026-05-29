using System.Linq;
using Ritmo.Core.Text;

namespace Ritmo.Core.Tests;

public class MarkdownLiteTests
{
    [Fact]
    public void Texto_plano_es_un_parrafo()
    {
        var blocks = MarkdownLite.Parse("Hola mundo");
        Assert.Single(blocks);
        Assert.Equal(MdBlockKind.Paragraph, blocks[0].Kind);
        Assert.Equal("Hola mundo", blocks[0].Inlines[0].Text);
    }

    [Theory]
    [InlineData("# Título", 1)]
    [InlineData("## Sub", 2)]
    [InlineData("### Sub sub", 3)]
    public void Encabezados_por_nivel(string md, int level)
    {
        var b = MarkdownLite.Parse(md).Single();
        Assert.Equal(MdBlockKind.Heading, b.Kind);
        Assert.Equal(level, b.Level);
    }

    [Fact]
    public void Vinetas_con_guion_o_asterisco()
    {
        var blocks = MarkdownLite.Parse("- uno\n* dos");
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.Equal(MdBlockKind.Bullet, b.Kind));
        Assert.Equal("uno", blocks[0].Inlines[0].Text);
        Assert.Equal("dos", blocks[1].Inlines[0].Text);
    }

    [Fact]
    public void Lineas_en_blanco_separan_no_crean_bloques()
    {
        var blocks = MarkdownLite.Parse("uno\n\n\ndos");
        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void Negrita_cursiva_y_codigo()
    {
        var inl = MarkdownLite.ParseInlines("normal **fuerte** y *flojo* y `cod`");
        Assert.Equal("normal ", inl[0].Text);
        Assert.True(inl[1].Bold);
        Assert.Equal("fuerte", inl[1].Text);
        Assert.True(inl[3].Italic);
        Assert.True(inl[5].Code);
    }

    [Fact]
    public void Cursiva_con_guion_bajo()
    {
        var inl = MarkdownLite.ParseInlines("_énfasis_");
        Assert.Single(inl);
        Assert.True(inl[0].Italic);
        Assert.Equal("énfasis", inl[0].Text);
    }

    [Fact]
    public void Enlace_texto_y_url()
    {
        var inl = MarkdownLite.ParseInlines("ver [campus](https://campus.zbrain.es) ya");
        Assert.Equal("ver ", inl[0].Text);
        Assert.Equal("campus", inl[1].Text);
        Assert.Equal("https://campus.zbrain.es", inl[1].Href);
        Assert.Equal(" ya", inl[2].Text);
    }

    [Fact]
    public void Vacio_da_lista_vacia()
    {
        Assert.Empty(MarkdownLite.Parse(""));
        Assert.Empty(MarkdownLite.Parse(null));
        Assert.Empty(MarkdownLite.ParseInlines(""));
    }

    [Fact]
    public void Negrita_dentro_de_vineta()
    {
        var b = MarkdownLite.Parse("- haz **esto**").Single();
        Assert.Equal(MdBlockKind.Bullet, b.Kind);
        Assert.Equal("haz ", b.Inlines[0].Text);
        Assert.True(b.Inlines[1].Bold);
    }
}
