using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ritmo.Core.Focus;

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
}
