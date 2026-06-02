using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Ritmo.Core.Sync;

/// <summary>
/// Piezas PURAS del login OAuth 2.0 de Google con PKCE (RFC 7636), para sincronizar Google Tasks
/// (#64). Genera el code_verifier/challenge y construye la URL de autorización; la red, el navegador
/// y el almacenamiento de tokens los hace el host. Apps de escritorio: redirect por loopback y, para
/// obtener refresh_token, <c>access_type=offline</c> + <c>prompt=consent</c>.
/// </summary>
public static class GoogleAuth
{
    public const string AuthorizeEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    public const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    /// <summary>Permiso para leer y escribir las listas/tareas de Google Tasks.</summary>
    public const string TasksScope = "https://www.googleapis.com/auth/tasks";

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

    /// <summary>Parsea la query del redirect de vuelta (<c>?code=…&amp;state=…</c>) en pares clave→valor.</summary>
    public static IReadOnlyDictionary<string, string> ParseQuery(string? pathAndQuery)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        var s = pathAndQuery ?? "";
        var q = s.IndexOf('?');
        if (q < 0) return dict;
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

    /// <summary>URL de autorización con PKCE (S256) + offline para recibir refresh_token.</summary>
    public static string AuthorizeUrl(string clientId, string redirectUri, string codeChallenge, string state, string scope = TasksScope)
    {
        var sb = new StringBuilder(AuthorizeEndpoint);
        sb.Append("?response_type=code");
        sb.Append("&client_id=").Append(Uri.EscapeDataString(clientId));
        sb.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
        sb.Append("&code_challenge_method=S256");
        sb.Append("&code_challenge=").Append(Uri.EscapeDataString(codeChallenge));
        sb.Append("&state=").Append(Uri.EscapeDataString(state));
        sb.Append("&scope=").Append(Uri.EscapeDataString(scope));
        sb.Append("&access_type=offline");   // necesario para recibir refresh_token
        sb.Append("&prompt=consent");         // fuerza el consentimiento → garantiza refresh_token
        return sb.ToString();
    }
}
