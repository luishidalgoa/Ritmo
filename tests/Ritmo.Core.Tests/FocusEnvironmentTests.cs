using Ritmo.Core.Focus;

namespace Ritmo.Core.Tests;

public class FocusEnvironmentTests
{
    [Fact]
    public void Entorno_completo_guarda_todas_las_acciones()
    {
        var env = new FocusEnvironment
        {
            Id = "simulacro",
            Name = "Simulacro",
            PomodoroPreset = "DeepWork",
            EnableDoNotDisturb = true,
            HideTaskbarBadges = true,
            ShowDayPreview = false,
            OpenStudyListInEdge = true,
            BlockedWebsites = ["youtube.com", "twitter.com", "reddit.com"],
            AppsToClose = ["Discord", "Steam"],
            AppsToMute = ["Spotify"],
            Music = new MusicLauncher { Name = "Aonsoku", Target = @"C:\Apps\Aonsoku.exe", AutoPlay = true }
        };

        Assert.Equal("Simulacro", env.Name);
        Assert.Equal(3, env.BlockedWebsites.Count);
        Assert.Contains("Discord", env.AppsToClose);
        Assert.Contains("Spotify", env.AppsToMute);
        Assert.Equal("Aonsoku", env.Music!.Name);
        Assert.True(env.Music.AutoPlay);
        Assert.True(env.OpenStudyListInEdge);
        Assert.False(env.ShowDayPreview);
    }

    [Fact]
    public void Valores_por_defecto_son_sensatos()
    {
        var env = new FocusEnvironment { Id = "x", Name = "Básico" };
        Assert.True(env.EnableDoNotDisturb);     // por defecto silenciar
        Assert.True(env.HideTaskbarBadges);
        Assert.True(env.ShowDayPreview);
        Assert.False(env.OpenStudyListInEdge);
        Assert.Empty(env.BlockedWebsites);
        Assert.Empty(env.AppsToClose);
        Assert.Empty(env.AppsToMute);
        Assert.Null(env.Music);
        Assert.Null(env.PomodoroPreset);          // usa el por defecto de la app
    }

    [Fact]
    public void Preset_DeepStudy_es_estudio_profundo()
    {
        var env = FocusEnvironment.DeepStudy;
        Assert.Equal("Estudio profundo", env.Name);
        Assert.Equal("DeepWork", env.PomodoroPreset);
        Assert.True(env.EnableDoNotDisturb);
    }

    [Fact]
    public void MusicLauncher_admite_uri_con_argumentos()
    {
        var m = new MusicLauncher
        {
            Name = "Spotify", Target = "spotify:",
            Arguments = "spotify:playlist:37i9dQZF1DX8Uebhn9wzrS", AutoPlay = true
        };
        Assert.Equal("spotify:", m.Target);
        Assert.Contains("playlist", m.Arguments);
    }

    [Fact]
    public void Dos_entornos_distintos_son_independientes()
    {
        var ligero = new FocusEnvironment { Id = "ligero", Name = "Repaso ligero", EnableDoNotDisturb = false };
        var profundo = FocusEnvironment.DeepStudy;
        Assert.NotEqual(ligero.Id, profundo.Id);
        Assert.False(ligero.EnableDoNotDisturb);
        Assert.True(profundo.EnableDoNotDisturb);
    }
}
