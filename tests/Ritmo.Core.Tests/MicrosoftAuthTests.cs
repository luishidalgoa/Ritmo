using System;
using Ritmo.Core.Sync;

namespace Ritmo.Core.Tests;

public class OAuthPkceTests
{
    [Fact]
    public void Verifier_longitud_en_rango_y_caracteres_validos()
    {
        var v = OAuthPkce.NewVerifier(64);
        Assert.Equal(64, v.Length);
        Assert.Equal(43, OAuthPkce.NewVerifier(10).Length);    // clamp inferior
        Assert.Equal(128, OAuthPkce.NewVerifier(999).Length);  // clamp superior
    }

    [Fact]
    public void Challenge_es_base64url_sin_relleno()
    {
        var c = OAuthPkce.Challenge("verifier-de-prueba");
        Assert.DoesNotContain("=", c);
        Assert.DoesNotContain("+", c);
        Assert.DoesNotContain("/", c);
    }

    [Fact]
    public void ParseQuery_extrae_code_y_state()
    {
        var q = OAuthPkce.ParseQuery("/?code=ABC123&state=xyz&extra=1");
        Assert.Equal("ABC123", q["code"]);
        Assert.Equal("xyz", q["state"]);
        Assert.Equal("1", q["extra"]);
    }

    [Fact]
    public void ParseQuery_sin_query_devuelve_vacio()
        => Assert.Empty(OAuthPkce.ParseQuery("/callback"));
}

public class MicrosoftAuthTests
{
    [Fact]
    public void AuthorizeUrl_incluye_pkce_y_parametros_clave()
    {
        var url = MicrosoftAuth.AuthorizeUrl("client-123", "http://localhost:51778/", "CHALLENGE", "STATE");
        Assert.StartsWith(MicrosoftAuth.AuthorizeEndpoint, url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("client_id=client-123", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("code_challenge=CHALLENGE", url);
        Assert.Contains("state=STATE", url);
        Assert.Contains(Uri.EscapeDataString("http://localhost:51778/"), url);
    }

    [Fact]
    public void Scope_pide_tasks_y_offline_access()
    {
        Assert.Contains("Tasks.ReadWrite", MicrosoftAuth.TasksScope);
        Assert.Contains("offline_access", MicrosoftAuth.TasksScope);   // necesario para el refresh token
    }
}
