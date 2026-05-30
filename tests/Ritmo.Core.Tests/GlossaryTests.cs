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
    public void Find_encuentra_claves_conocidas(string key)
        => Assert.NotNull(Glossary.Find(key));

    [Fact]
    public void Find_desconocida_da_null()
        => Assert.Null(Glossary.Find("no-existe"));
}
