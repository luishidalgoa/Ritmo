using System.Collections.Generic;
using Ritmo.Core.Focus;

namespace Ritmo.Core.Tests;

public class SubsonicTests
{
    [Fact]
    public void Token_md5_password_mas_salt()
    {
        // Vector del ejemplo oficial de la API Subsonic: password "sesame", salt "c19b2d".
        Assert.Equal("26719a1196d2a940705a59634eb18eab", Subsonic.Token("sesame", "c19b2d"));
    }

    [Fact]
    public void Token_es_hex_minusculas_de_32()
    {
        var t = Subsonic.Token("hunter2", "abc");
        Assert.Equal(32, t.Length);
        Assert.Equal(t.ToLowerInvariant(), t);
    }

    [Theory]
    [InlineData("https://music.example.com", "https://music.example.com")]
    [InlineData("https://music.example.com/", "https://music.example.com")]
    [InlineData("music.example.com:4533", "https://music.example.com:4533")]
    [InlineData("https://nav.example.com/rest", "https://nav.example.com")]
    [InlineData("  http://192.168.1.10:4533/  ", "http://192.168.1.10:4533")]
    public void NormalizeServerUrl(string raw, string expected)
        => Assert.Equal(expected, Subsonic.NormalizeServerUrl(raw));

    [Fact]
    public void BuildUrl_incluye_auth_y_formato()
    {
        var url = Subsonic.BuildUrl("https://music.example.com/", "getPlaylists", "luis", "tok", "sal");
        Assert.StartsWith("https://music.example.com/rest/getPlaylists?", url);
        Assert.Contains("u=luis", url);
        Assert.Contains("t=tok", url);
        Assert.Contains("s=sal", url);
        Assert.Contains("v=1.16.1", url);
        Assert.Contains("c=Ritmo", url);
        Assert.Contains("f=json", url);
    }

    [Fact]
    public void BuildUrl_anade_parametros_extra_escapados()
    {
        var extra = new[] { new KeyValuePair<string, string>("id", "pl 7&x") };
        var url = Subsonic.BuildUrl("https://m.example.com", "stream", "u", "t", "s", extra: extra);
        Assert.Contains("&id=pl%207%26x", url);
    }

    [Fact]
    public void ParsePlaylists_array()
    {
        const string json = """
        { "subsonic-response": { "status":"ok", "playlists": { "playlist": [
            { "id":"a1", "name":"Estudio", "songCount":42, "owner":"luis", "coverArt":"pl-a1" },
            { "id":"b2", "name":"Foco", "songCount":7, "owner":"luis" }
        ] } } }
        """;
        var pls = Subsonic.ParsePlaylists(json);
        Assert.Equal(2, pls.Count);
        Assert.Equal("Estudio", pls[0].Name);
        Assert.Equal(42, pls[0].SongCount);
        Assert.Equal("pl-a1", pls[0].CoverArt);
        Assert.Null(pls[1].CoverArt);
    }

    [Fact]
    public void ParsePlaylists_objeto_unico()
    {
        const string json = """
        { "subsonic-response": { "playlists": { "playlist":
            { "id":"x", "name":"Sola", "songCount":3, "owner":"luis" }
        } } }
        """;
        var pls = Subsonic.ParsePlaylists(json);
        Assert.Single(pls);
        Assert.Equal("Sola", pls[0].Name);
    }

    [Fact]
    public void ParsePlaylists_vacio_o_invalido()
    {
        Assert.Empty(Subsonic.ParsePlaylists(null));
        Assert.Empty(Subsonic.ParsePlaylists("no es json"));
        Assert.Empty(Subsonic.ParsePlaylists("""{ "subsonic-response": { "status":"failed" } }"""));
    }
}
