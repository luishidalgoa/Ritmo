using Ritmo.Core.Focus;

namespace Ritmo.Core.Tests;

public class MusicAndWebTests
{
    // ---------- Spotify ----------

    [Fact]
    public void SpotifyTarget_vacio_abre_spotify()
    {
        Assert.Equal("spotify:", MusicLaunch.SpotifyTarget(null));
        Assert.Equal("spotify:", MusicLaunch.SpotifyTarget("   "));
    }

    [Fact]
    public void SpotifyTarget_uri_se_conserva()
        => Assert.Equal("spotify:playlist:abc123", MusicLaunch.SpotifyTarget("spotify:playlist:abc123"));

    [Theory]
    [InlineData("https://open.spotify.com/playlist/37i9dQZF1DX", "spotify:playlist:37i9dQZF1DX")]
    [InlineData("https://open.spotify.com/playlist/37i9dQZF1DX?si=abcdef", "spotify:playlist:37i9dQZF1DX")]
    [InlineData("https://open.spotify.com/intl-es/playlist/XYZ", "spotify:playlist:XYZ")]
    [InlineData("https://open.spotify.com/album/ALB?x=1", "spotify:album:ALB")]
    public void SpotifyTarget_convierte_url_a_uri(string url, string expected)
        => Assert.Equal(expected, MusicLaunch.SpotifyTarget(url));

    [Fact]
    public void SpotifyTarget_desconocido_se_pasa_tal_cual()
        => Assert.Equal("algo-raro", MusicLaunch.SpotifyTarget("algo-raro"));

    // ---------- Dominios web ----------

    [Theory]
    [InlineData("youtube.com", "youtube.com")]
    [InlineData("https://www.youtube.com/feed/subscriptions", "youtube.com")]
    [InlineData("http://YouTube.com", "youtube.com")]
    [InlineData("www.twitter.com", "twitter.com")]
    [InlineData("https://user:pass@example.com:8080/path?q=1", "example.com")]
    [InlineData("  reddit.com  ", "reddit.com")]
    public void WebDomain_normaliza(string raw, string expected)
        => Assert.Equal(expected, WebDomain.Normalize(raw));

    [Fact]
    public void WebDomain_vacio_da_vacio()
    {
        Assert.Equal("", WebDomain.Normalize(null));
        Assert.Equal("", WebDomain.Normalize("   "));
    }
}
