using System.Linq;
using Ritmo.Core.Focus;

namespace Ritmo.Core.Tests;

public class SpotifyAuthTests
{
    [Fact]
    public void Challenge_vector_RFC7636()
    {
        // Ejemplo del apéndice B de la RFC 7636.
        Assert.Equal(
            "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            SpotifyAuth.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void NewVerifier_longitud_y_charset()
    {
        var v = SpotifyAuth.NewVerifier();
        Assert.InRange(v.Length, 43, 128);
        const string ok = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        Assert.All(v, c => Assert.Contains(c, ok));
    }

    [Fact]
    public void NewVerifier_recorta_longitudes_fuera_de_rango()
    {
        Assert.Equal(43, SpotifyAuth.NewVerifier(10).Length);
        Assert.Equal(128, SpotifyAuth.NewVerifier(500).Length);
    }

    [Fact]
    public void Base64Url_sin_relleno_ni_caracteres_inseguros()
    {
        var s = SpotifyAuth.Base64Url([0xFF, 0xFE, 0xFD, 0xFC, 0xFB]);
        Assert.DoesNotContain('=', s);
        Assert.DoesNotContain('+', s);
        Assert.DoesNotContain('/', s);
    }

    [Fact]
    public void ParseQuery_extrae_code_y_state()
    {
        var q = SpotifyAuth.ParseQuery("/callback?code=AQ%3D%3D&state=abc123");
        Assert.Equal("AQ==", q["code"]);
        Assert.Equal("abc123", q["state"]);
    }

    [Fact]
    public void ParseQuery_maneja_error_y_vacio()
    {
        Assert.Equal("access_denied", SpotifyAuth.ParseQuery("/callback?error=access_denied")["error"]);
        Assert.Empty(SpotifyAuth.ParseQuery("/callback"));
        Assert.Empty(SpotifyAuth.ParseQuery(""));
    }

    [Fact]
    public void AuthorizeUrl_incluye_parametros_pkce()
    {
        var url = SpotifyAuth.AuthorizeUrl("CID", "http://127.0.0.1:43117/callback", "CHAL", "ST");
        Assert.StartsWith("https://accounts.spotify.com/authorize?", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("client_id=CID", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("code_challenge=CHAL", url);
        Assert.Contains("state=ST", url);
        Assert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A43117%2Fcallback", url);
        Assert.Contains("scope=playlist-read-private", url);
    }
}
