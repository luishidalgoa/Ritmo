using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ritmo.Core.Focus;

/// <summary>Una playlist devuelta por un servidor Subsonic/Navidrome. #107</summary>
public sealed record SubsonicPlaylist(string Id, string Name, int SongCount, string Owner, string? CoverArt);

/// <summary>
/// Helpers puros para hablar con un servidor compatible con la API Subsonic
/// (Navidrome). Auth por token: <c>token = md5(password + salt)</c>, así la
/// contraseña no viaja en claro. Solo construcción de URLs/token; la red la hace
/// el host. #107
/// </summary>
public static class Subsonic
{
    /// <summary>Token de autenticación Subsonic: md5(password + salt) en hex minúsculas.</summary>
    public static string Token(string password, string salt)
    {
        var bytes = Encoding.UTF8.GetBytes((password ?? "") + (salt ?? ""));
        var hash = MD5.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    /// <summary>
    /// Normaliza la URL del servidor: recorta espacios y la barra final, quita un
    /// sufijo <c>/rest</c> si lo pegaron, y antepone <c>https://</c> si falta esquema.
    /// </summary>
    public static string NormalizeServerUrl(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (s.Length == 0) return "";
        if (!s.Contains("://")) s = "https://" + s;
        s = s.TrimEnd('/');
        if (s.EndsWith("/rest", System.StringComparison.OrdinalIgnoreCase)) s = s[..^5].TrimEnd('/');
        return s;
    }

    /// <summary>
    /// Construye la URL de un endpoint Subsonic con autenticación por token
    /// (p. ej. <c>getPlaylists</c>, <c>stream</c>). Devuelve JSON (<c>f=json</c>).
    /// </summary>
    public static string BuildUrl(
        string serverUrl, string endpoint, string user, string token, string salt,
        string clientName = "Ritmo", string apiVersion = "1.16.1",
        IEnumerable<KeyValuePair<string, string>>? extra = null)
    {
        var baseUrl = NormalizeServerUrl(serverUrl);
        var sb = new StringBuilder();
        sb.Append(baseUrl).Append("/rest/").Append(endpoint)
          .Append("?u=").Append(Esc(user))
          .Append("&t=").Append(Esc(token))
          .Append("&s=").Append(Esc(salt))
          .Append("&v=").Append(Esc(apiVersion))
          .Append("&c=").Append(Esc(clientName))
          .Append("&f=json");
        if (extra is not null)
            foreach (var kv in extra)
                sb.Append('&').Append(Esc(kv.Key)).Append('=').Append(Esc(kv.Value));
        return sb.ToString();
    }

    private static string Esc(string? v) => System.Uri.EscapeDataString(v ?? "");

    /// <summary>
    /// Parsea la respuesta JSON de <c>getPlaylists</c>. Tolera que <c>playlist</c>
    /// sea un array o un único objeto (rareza del JSON de Subsonic). Devuelve vacío
    /// si la respuesta no es "ok" o no hay playlists.
    /// </summary>
    public static IReadOnlyList<SubsonicPlaylist> ParsePlaylists(string? json)
    {
        var list = new List<SubsonicPlaylist>();
        if (string.IsNullOrWhiteSpace(json)) return list;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("subsonic-response", out var resp)) return list;
            if (!resp.TryGetProperty("playlists", out var playlists)) return list;
            if (!playlists.TryGetProperty("playlist", out var pl)) return list;

            if (pl.ValueKind == JsonValueKind.Array)
                foreach (var p in pl.EnumerateArray()) list.Add(ParseOne(p));
            else if (pl.ValueKind == JsonValueKind.Object)
                list.Add(ParseOne(pl));
        }
        catch (JsonException) { /* respuesta no válida: lista vacía */ }
        return list;
    }

    private static SubsonicPlaylist ParseOne(JsonElement p)
    {
        string id = p.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
        string name = p.TryGetProperty("name", out var n) ? n.GetString() ?? "(sin nombre)" : "(sin nombre)";
        int songs = p.TryGetProperty("songCount", out var sc) && sc.TryGetInt32(out var v) ? v : 0;
        string owner = p.TryGetProperty("owner", out var o) ? o.GetString() ?? "" : "";
        string? cover = p.TryGetProperty("coverArt", out var c) ? c.GetString() : null;
        return new SubsonicPlaylist(id, name, songs, owner, cover);
    }
}
