using System;
using System.Security.Cryptography;
using System.Text;

namespace Ritmo.Core.Focus;

/// <summary>
/// Piezas puras del login OAuth 2.0 de Spotify (PKCE, RFC 7636): generación del
/// code_verifier/challenge y construcción de la URL de autorización. La red, el
/// navegador y el almacenamiento de tokens los hace el host. #106
/// </summary>
public static class SpotifyAuth
{
    public const string AuthorizeEndpoint = "https://accounts.spotify.com/authorize";
    public const string TokenEndpoint = "https://accounts.spotify.com/api/token";
    public const string PlaylistsEndpoint = "https://api.spotify.com/v1/me/playlists";

    /// <summary>Permisos mínimos para leer las playlists del usuario.</summary>
    public const string Scope = "playlist-read-private playlist-read-collaborative";

    // Juego de caracteres no reservados permitidos en un code_verifier PKCE.
    private const string VerifierChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    /// <summary>Genera un code_verifier PKCE (longitud 43–128 del juego permitido).</summary>
    public static string NewVerifier(int length = 64)
    {
        length = Math.Clamp(length, 43, 128);
        var buf = new byte[length];
        RandomNumberGenerator.Fill(buf);
        var sb = new StringBuilder(length);
        foreach (var b in buf) sb.Append(VerifierChars[b % VerifierChars.Length]);
        return sb.ToString();
    }

    /// <summary>Valor aleatorio para el parámetro <c>state</c> (anti-CSRF).</summary>
    public static string NewState() => Base64Url(RandomNumberGenerator.GetBytes(16));

    /// <summary>code_challenge = base64url(sha256(ascii(verifier))) sin relleno.</summary>
    public static string Challenge(string verifier)
        => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>Base64 "url-safe" sin relleno.</summary>
    public static string Base64Url(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Parsea la query del redirect de vuelta (p. ej. <c>/callback?code=…&amp;state=…</c>)
    /// en pares clave→valor ya des-escapados. Lo usa el host al recibir el callback.
    /// </summary>
    public static System.Collections.Generic.IReadOnlyDictionary<string, string> ParseQuery(string? pathAndQuery)
    {
        var dict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        var s = pathAndQuery ?? "";
        var q = s.IndexOf('?');
        if (q < 0) return dict;          // sin query string
        s = s[(q + 1)..];
        var hash = s.IndexOf('#');
        if (hash >= 0) s = s[..hash];
        foreach (var pair in s.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) { dict[Uri.UnescapeDataString(pair)] = ""; continue; }
            dict[Uri.UnescapeDataString(pair[..eq])] = Uri.UnescapeDataString(pair[(eq + 1)..]);
        }
        return dict;
    }

    /// <summary>URL de autorización con PKCE (method=S256).</summary>
    public static string AuthorizeUrl(string clientId, string redirectUri, string codeChallenge, string state, string scope = Scope)
    {
        var sb = new StringBuilder(AuthorizeEndpoint);
        sb.Append("?response_type=code");
        sb.Append("&client_id=").Append(Uri.EscapeDataString(clientId));
        sb.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
        sb.Append("&code_challenge_method=S256");
        sb.Append("&code_challenge=").Append(Uri.EscapeDataString(codeChallenge));
        sb.Append("&state=").Append(Uri.EscapeDataString(state));
        sb.Append("&scope=").Append(Uri.EscapeDataString(scope));
        return sb.ToString();
    }
}
