using System.Collections.Generic;
using Ritmo.Core.Focus;

namespace Ritmo.Core.Tests;

public class EdgeProfileResolverTests
{
    [Fact]
    public void Reutiliza_perfil_existente_llamado_Estudio()
    {
        var profiles = new Dictionary<string, string>
        {
            ["Default"] = "Personal",
            ["Profile 1"] = "Estudio",
        };

        var d = EdgeProfileResolver.Resolve(profiles, "es-ES");

        Assert.False(d.NeedsCreation);
        Assert.Equal("Profile 1", d.Folder);
        Assert.Equal("Estudio", d.DisplayName);
    }

    [Fact]
    public void Reutiliza_perfil_en_ingles_Study()
    {
        var profiles = new Dictionary<string, string>
        {
            ["Profile 2"] = "My Study Profile",
        };

        var d = EdgeProfileResolver.Resolve(profiles, "en-US");

        Assert.False(d.NeedsCreation);
        Assert.Equal("Profile 2", d.Folder);
    }

    [Theory]
    [InlineData("Focus")]
    [InlineData("Concentración")]
    [InlineData("Deep Work")]
    [InlineData("Trabajo")]
    public void Reconoce_variantes_de_concentracion(string name)
    {
        var profiles = new Dictionary<string, string> { ["Profile 1"] = name };

        var d = EdgeProfileResolver.Resolve(profiles, "es-ES");

        Assert.False(d.NeedsCreation);
        Assert.Equal("Profile 1", d.Folder);
    }

    [Fact]
    public void Crea_Estudio_cuando_idioma_es_espanol()
    {
        var profiles = new Dictionary<string, string>
        {
            ["Default"] = "Perfil 1",
            ["Profile 1"] = "Perfil 2",
        };

        var d = EdgeProfileResolver.Resolve(profiles, "es-ES");

        Assert.True(d.NeedsCreation);
        Assert.Equal("Estudio", d.DisplayName);
        Assert.Equal("Profile 2", d.Folder);   // siguiente libre (no choca con Profile 1)
    }

    [Fact]
    public void Crea_Study_cuando_idioma_es_ingles()
    {
        var profiles = new Dictionary<string, string> { ["Default"] = "Person" };

        var d = EdgeProfileResolver.Resolve(profiles, "en-US");

        Assert.True(d.NeedsCreation);
        Assert.Equal("Study", d.DisplayName);
        Assert.Equal("Profile 1", d.Folder);
    }

    [Fact]
    public void No_reutiliza_un_perfil_cualquiera_que_no_sea_de_estudio()
    {
        // "Perfil"/"Personal"/"Trabajos escolares" no deben confundirse.
        var profiles = new Dictionary<string, string>
        {
            ["Default"] = "Personal",
            ["Profile 1"] = "Perfil 2",
        };

        var d = EdgeProfileResolver.Resolve(profiles, "es-ES");

        Assert.True(d.NeedsCreation);
    }

    [Fact]
    public void Evita_colisiones_con_carpetas_en_disco()
    {
        var profiles = new Dictionary<string, string> { ["Default"] = "Personal" };
        // Profile 1 y Profile 2 existen en disco aunque no estén en el cache.
        var onDisk = new[] { "Default", "Profile 1", "Profile 2" };

        var d = EdgeProfileResolver.Resolve(profiles, "es-ES", onDisk);

        Assert.True(d.NeedsCreation);
        Assert.Equal("Profile 3", d.Folder);
    }

    [Fact]
    public void Sin_perfiles_crea_Profile_1()
    {
        var d = EdgeProfileResolver.Resolve(new Dictionary<string, string>(), "es");

        Assert.True(d.NeedsCreation);
        Assert.Equal("Profile 1", d.Folder);
        Assert.Equal("Estudio", d.DisplayName);
    }

    [Theory]
    [InlineData("es", "Estudio")]
    [InlineData("es-ES", "Estudio")]
    [InlineData("en", "Study")]
    [InlineData("en-US", "Study")]
    [InlineData("fr-FR", "Study")]
    [InlineData("", "Study")]
    public void StudyNameFor_segun_idioma(string lang, string expected)
        => Assert.Equal(expected, EdgeProfileResolver.StudyNameFor(lang));

    [Fact]
    public void IsStudyName_ignora_mayusculas()
    {
        Assert.True(EdgeProfileResolver.IsStudyName("ESTUDIO"));
        Assert.True(EdgeProfileResolver.IsStudyName("Mi Focus"));
        Assert.False(EdgeProfileResolver.IsStudyName("Personal"));
        Assert.False(EdgeProfileResolver.IsStudyName(""));
        Assert.False(EdgeProfileResolver.IsStudyName(null));
    }
}
