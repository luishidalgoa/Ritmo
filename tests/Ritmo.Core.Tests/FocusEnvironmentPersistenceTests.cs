using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

public class FocusEnvironmentPersistenceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public FocusEnvironmentPersistenceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "RitmoEnv_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    private static AppSettings WithEnvironments() => new()
    {
        FocusEnvironments =
        [
            new FocusEnvironment
            {
                Id = "deep", Name = "Estudio profundo", PomodoroPreset = "DeepWork",
                BlockedWebsites = ["youtube.com", "reddit.com"],
                AppsToClose = ["Discord"],
                AppsToMute = ["Spotify"],
                OpenLinksInBrowser = true,
                Music = new MusicLauncher { Name = "Aonsoku", Target = @"C:\Apps\Aonsoku.exe", AutoPlay = true }
            },
            new FocusEnvironment { Id = "ligero", Name = "Repaso ligero", EnableDoNotDisturb = false }
        ],
        DefaultFocusEnvironmentId = "deep",
        EnvironmentByKind = new Dictionary<string, string>
        {
            ["Simulacro"] = "ligero"
        }
    };

    [Fact]
    public void RoundTrip_de_entornos()
    {
        var store = new JsonSettingsStore(_file);
        store.Save(WithEnvironments());
        var loaded = store.Load();

        Assert.Equal(2, loaded.FocusEnvironments.Count);
        var deep = loaded.FocusEnvironments.First(e => e.Id == "deep");
        Assert.Equal("Estudio profundo", deep.Name);
        Assert.Equal(new[] { "youtube.com", "reddit.com" }, deep.BlockedWebsites.ToArray());
        Assert.Contains("Discord", deep.AppsToClose);
        Assert.Contains("Spotify", deep.AppsToMute);
        Assert.True(deep.OpenLinksInBrowser);
        Assert.Equal("Aonsoku", deep.Music!.Name);
        Assert.True(deep.Music.AutoPlay);

        Assert.Equal("deep", loaded.DefaultFocusEnvironmentId);
    }

    [Fact]
    public void RoundTrip_musica_navidrome_y_conexion_global()
    {
        var store = new JsonSettingsStore(_file);
        store.Save(new AppSettings
        {
            NavidromeServerUrl = "https://music.example.com",
            NavidromeUser = "luis",
            FocusEnvironments =
            [
                new FocusEnvironment
                {
                    Id = "n", Name = "Con Navidrome",
                    Music = new MusicLauncher
                    {
                        Name = "Navidrome", Provider = "navidrome",
                        PlaylistId = "pl-7", PlaylistName = "Foco",
                        Target = "https://music.example.com/app/#/playlist/pl-7/show"
                    }
                }
            ]
        });
        var loaded = store.Load();
        Assert.Equal("https://music.example.com", loaded.NavidromeServerUrl);
        Assert.Equal("luis", loaded.NavidromeUser);
        var m = loaded.FocusEnvironments.Single().Music!;
        Assert.Equal("navidrome", m.Provider);
        Assert.Equal("pl-7", m.PlaylistId);
        Assert.Equal("Foco", m.PlaylistName);
    }

    [Fact]
    public void RoundTrip_appsToOpen_y_escritorio_virtual()
    {
        var store = new JsonSettingsStore(_file);
        store.Save(new AppSettings
        {
            FocusEnvironments =
            [
                new FocusEnvironment
                {
                    Id = "e", Name = "Estudio",
                    AppsToOpen = ["onenote", "Code"],
                    NewVirtualDesktop = true
                }
            ]
        });
        var env = store.Load().FocusEnvironments.Single();
        Assert.Equal(["onenote", "Code"], env.AppsToOpen.ToArray());
        Assert.True(env.NewVirtualDesktop);
    }

    [Fact]
    public void ResolveEnvironment_usa_mapeo_por_tipo_y_luego_default()
    {
        var store = new JsonSettingsStore(_file);
        store.Save(WithEnvironments());
        var loaded = store.Load();

        // Simulacro está mapeado a "ligero".
        Assert.Equal("ligero", loaded.ResolveEnvironment("Simulacro")!.Id);
        // Técnico no está mapeado -> cae al por defecto "deep".
        Assert.Equal("deep", loaded.ResolveEnvironment("Tecnico")!.Id);
    }

    [Fact]
    public void Sin_entornos_ResolveEnvironment_es_null()
    {
        var loaded = new JsonSettingsStore(_file).Load(); // archivo inexistente -> Default
        Assert.Empty(loaded.FocusEnvironments);
        Assert.Null(loaded.ResolveEnvironment("Tecnico"));
    }
}
