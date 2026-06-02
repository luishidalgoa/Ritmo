using System;
using Ritmo.Core.Sync;

namespace Ritmo.Core.Tests;

public class GoogleAuthTests
{
    [Fact]
    public void Verifier_respeta_longitud_minima_y_maxima()
    {
        Assert.Equal(43, GoogleAuth.NewVerifier(10).Length);   // por debajo del mínimo → 43
        Assert.Equal(128, GoogleAuth.NewVerifier(999).Length); // por encima del máximo → 128
        Assert.Equal(64, GoogleAuth.NewVerifier().Length);
    }

    [Fact]
    public void Challenge_es_determinista_y_base64url_sin_relleno()
    {
        var v = "test-verifier-123";
        var c1 = GoogleAuth.Challenge(v);
        var c2 = GoogleAuth.Challenge(v);
        Assert.Equal(c1, c2);
        Assert.DoesNotContain("=", c1);
        Assert.DoesNotContain("+", c1);
        Assert.DoesNotContain("/", c1);
    }

    [Fact]
    public void ParseQuery_extrae_code_y_state()
    {
        var q = GoogleAuth.ParseQuery("/callback?code=abc%2F123&state=xyz&scope=tasks");
        Assert.Equal("abc/123", q["code"]);   // des-escapado
        Assert.Equal("xyz", q["state"]);
    }

    [Fact]
    public void ParseQuery_sin_query_devuelve_vacio()
        => Assert.Empty(GoogleAuth.ParseQuery("/callback"));

    [Fact]
    public void AuthorizeUrl_incluye_pkce_offline_y_scope()
    {
        var url = GoogleAuth.AuthorizeUrl("CID.apps.googleusercontent.com", "http://127.0.0.1:51777/callback",
            "CHAL", "STATE");
        Assert.StartsWith(GoogleAuth.AuthorizeEndpoint, url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("code_challenge=CHAL", url);
        Assert.Contains("access_type=offline", url);   // imprescindible para refresh_token
        Assert.Contains("prompt=consent", url);
        Assert.Contains(Uri.EscapeDataString(GoogleAuth.TasksScope), url);
        Assert.Contains(Uri.EscapeDataString("http://127.0.0.1:51777/callback"), url);
    }
}
