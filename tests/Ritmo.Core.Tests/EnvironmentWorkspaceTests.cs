using System.Linq;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

/// <summary>Workspace de un entorno (#78): qué URLs se abren en el navegador.</summary>
public class EnvironmentWorkspaceTests
{
    private static FocusEnvironment WithLinks(params string[] urls) => new()
    {
        Id = "e", Name = "e",
        Links = urls.Select((u, i) => new ShortcutLink { Title = $"L{i}", Url = u }).ToList()
    };

    [Fact]
    public void Sin_enlaces_no_se_puede_abrir()
    {
        var env = new FocusEnvironment { Id = "e", Name = "e" };
        Assert.Empty(EnvironmentWorkspace.Urls(env));
        Assert.False(EnvironmentWorkspace.CanOpen(env));
    }

    [Fact]
    public void Conserva_orden_y_descarta_vacios()
    {
        var env = WithLinks("https://a.com", "   ", "https://b.com");
        Assert.Equal(["https://a.com", "https://b.com"], EnvironmentWorkspace.Urls(env).ToArray());
        Assert.True(EnvironmentWorkspace.CanOpen(env));
    }

    [Fact]
    public void Quita_duplicados_ignorando_mayusculas()
    {
        var env = WithLinks("https://A.com", "https://a.com", "https://b.com");
        Assert.Equal(["https://A.com", "https://b.com"], EnvironmentWorkspace.Urls(env).ToArray());
    }

    [Fact]
    public void Normaliza_con_trim()
        => Assert.Equal(["https://a.com"], EnvironmentWorkspace.Urls(WithLinks("  https://a.com  ")).ToArray());
}
