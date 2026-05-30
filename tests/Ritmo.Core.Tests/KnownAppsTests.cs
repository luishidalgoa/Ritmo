using System.Linq;
using Ritmo.Core.Focus;

namespace Ritmo.Core.Tests;

public class KnownAppsTests
{
    [Fact]
    public void Catalogo_no_vacio()
        => Assert.NotEmpty(KnownApps.Catalog);

    [Fact]
    public void Toda_app_tiene_nombre_proceso_y_url()
        => Assert.All(KnownApps.Catalog, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Name));
            Assert.False(string.IsNullOrWhiteSpace(a.ProcessName));
            Assert.StartsWith("http", a.InstallUrl);
            Assert.False(string.IsNullOrWhiteSpace(a.MatchTerm));
        });

    [Fact]
    public void Procesos_unicos()
        => Assert.Equal(KnownApps.Catalog.Count,
                        KnownApps.Catalog.Select(a => a.ProcessName.ToLowerInvariant()).Distinct().Count());

    [Fact]
    public void ByCategory_cubre_todas_las_apps()
    {
        var total = KnownApps.ByCategory().Sum(g => g.Apps.Count);
        Assert.Equal(KnownApps.Catalog.Count, total);
    }

    [Fact]
    public void ByProcess_no_distingue_mayusculas()
    {
        Assert.NotNull(KnownApps.ByProcess("DISCORD"));
        Assert.NotNull(KnownApps.ByProcess("spotify"));
        Assert.Null(KnownApps.ByProcess("no-existe"));
    }

    [Fact]
    public void Hay_varias_categorias()
        => Assert.True(KnownApps.ByCategory().Count >= 3);

    [Fact]
    public void Hay_categoria_productividad_con_target_de_lanzamiento()
    {
        var prod = KnownApps.Catalog.Where(a => a.Category == AppCategory.Productividad).ToList();
        Assert.NotEmpty(prod);
        // Al menos algunas traen un objetivo de lanzamiento (protocolo o nombre App Paths).
        Assert.Contains(prod, a => !string.IsNullOrEmpty(a.LaunchTarget));
        var onenote = KnownApps.ByProcess("onenote");
        Assert.NotNull(onenote);
        Assert.Equal("onenote:", onenote!.LaunchTarget);
    }
}
