using Ritmo.Core.Commands;
using Ritmo.Core.Updates;

namespace Ritmo.Core.Tests;

/// <summary>Novedades / carrusel de actualización: comparación de versión + selección + persistencia.</summary>
public class ReleaseNotesTests
{
    [Theory]
    [InlineData("1.0.1.0", "1.0.1.0", 0)]
    [InlineData("1.0.2.0", "1.0.1.0", 1)]
    [InlineData("1.0.0.0", "1.0.1.0", -1)]
    [InlineData("2.0", "1.9.9.9", 1)]     // formato corto
    [InlineData("1", "1.0.0.0", 0)]
    [InlineData("", "0.0.0.0", 0)]        // vacío = 0
    [InlineData("1.0.1.5", "1.0.1.0", 1)] // revision
    public void CompareVersions(string a, string b, int expectedSign)
        => Assert.Equal(expectedSign, System.Math.Sign(ReleaseNotes.CompareVersions(a, b)));

    [Fact]
    public void Since_primera_vez_muestra_lo_de_hasta_la_version_actual()
    {
        var notes = ReleaseNotes.Since(null, "1.0.1.0");
        Assert.NotEmpty(notes);
        Assert.Contains(notes, n => n.Version == "1.0.1.0");
    }

    [Fact]
    public void Since_si_ya_vio_la_actual_no_hay_novedades()
        => Assert.Empty(ReleaseNotes.Since("1.0.1.0", "1.0.1.0"));

    [Fact]
    public void Since_no_incluye_versiones_por_encima_de_la_actual()
    {
        // Aunque hubiera notas futuras, no se muestran si la app aún no está en esa versión.
        var notes = ReleaseNotes.Since(null, "1.0.0.0");
        Assert.DoesNotContain(notes, n => ReleaseNotes.CompareVersions(n.Version, "1.0.0.0") > 0);
    }

    [Fact]
    public void Las_notas_tienen_contenido_de_usuario()
    {
        foreach (var n in ReleaseNotes.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(n.Version));
            Assert.False(string.IsNullOrWhiteSpace(n.Title));
            Assert.NotEmpty(n.Highlights);
        }
    }

    [Fact]
    public void SetLastSeenVersion_persiste_y_sobrevive_round_trip()
    {
        var svc = new ConfigurationService(new InMemorySettingsStore());
        svc.SetLastSeenVersion("1.0.1.0");
        Assert.Equal("1.0.1.0", svc.GetSettings().LastSeenVersion);

        var svc2 = new ConfigurationService(new InMemorySettingsStore());
        svc2.ImportJson(svc.ExportJson());
        Assert.Equal("1.0.1.0", svc2.GetSettings().LastSeenVersion);
    }
}
