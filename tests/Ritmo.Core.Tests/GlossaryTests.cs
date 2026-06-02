using System.Linq;
using Ritmo.Core.Help;

namespace Ritmo.Core.Tests;

public class GlossaryTests
{
    [Fact]
    public void Hay_entradas()
        => Assert.NotEmpty(Glossary.Entries);

    [Fact]
    public void Claves_unicas()
        => Assert.Equal(Glossary.Entries.Count, Glossary.Entries.Select(e => e.Key).Distinct().Count());

    [Fact]
    public void Toda_entrada_tiene_termino_y_descripcion()
        => Assert.All(Glossary.Entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Term));
            Assert.False(string.IsNullOrWhiteSpace(e.Description));
        });

    [Theory]
    [InlineData("pomodoro")]
    [InlineData("deep-work")]
    [InlineData("prealert")]
    [InlineData("environment")]
    // Conceptos nuevos con tooltip enriquecido (#93).
    [InlineData("category")]
    [InlineData("focus-category")]
    [InlineData("default-prealert")]
    [InlineData("oneoff")]
    [InlineData("rest-mode")]
    [InlineData("work-tracking")]
    [InlineData("work-rate")]
    [InlineData("work-goal")]
    [InlineData("work-auto")]
    [InlineData("work-link")]
    [InlineData("session-exception")]
    public void Find_encuentra_claves_conocidas(string key)
        => Assert.NotNull(Glossary.Find(key));

    [Fact]
    public void Find_desconocida_da_null()
        => Assert.Null(Glossary.Find("no-existe"));

    // ---------- i18n (#48): el inglés debe cubrir EXACTAMENTE las mismas claves ----------

    [Fact]
    public void Ingles_cubre_las_mismas_claves_que_espanol()
    {
        var es = Glossary.Entries.Select(e => e.Key).OrderBy(k => k).ToList();
        var en = Glossary.EntriesEn.Select(e => e.Key).OrderBy(k => k).ToList();
        Assert.Equal(es, en);
    }

    [Fact]
    public void Toda_entrada_en_ingles_tiene_termino_y_descripcion()
        => Assert.All(Glossary.EntriesEn, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Term));
            Assert.False(string.IsNullOrWhiteSpace(e.Description));
        });

    [Fact]
    public void For_en_devuelve_el_glosario_ingles_y_es_el_espanol()
    {
        Assert.Same(Glossary.EntriesEn, Glossary.For("en"));
        Assert.Same(Glossary.Entries, Glossary.For("es"));
        Assert.Same(Glossary.Entries, Glossary.For("system"));   // por defecto, español
    }

    [Fact]
    public void Find_con_idioma_ingles_devuelve_el_termino_traducido()
    {
        var es = Glossary.Find("pomodoro", "es");
        var en = Glossary.Find("pomodoro", "en");
        Assert.NotNull(es);
        Assert.NotNull(en);
        Assert.NotEqual(es!.Description, en!.Description);   // están traducidas
    }
}
