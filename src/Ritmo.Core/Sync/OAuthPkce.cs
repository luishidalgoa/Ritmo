using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Ritmo.Core.Sync;

/// <summary>
/// Piezas PURAS y reutilizables del flujo OAuth 2.0 con PKCE (RFC 7636) para apps de escritorio
/// (#64): generación de code_verifier/challenge, valor de <c>state</c> anti-CSRF y parseo del
/// redirect de loopback. Lo comparten los proveedores (Google, Microsoft…). La red, el navegador
/// y el almacenamiento de tokens los hace el host.
/// </summary>
public static class OAuthPkce
{
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
}
