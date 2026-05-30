namespace Ritmo.Core.Focus;

/// <summary>
/// Normaliza lo que el usuario escribe como "web a bloquear" a un dominio limpio
/// (sin esquema, sin www., sin ruta/puerto/credenciales, en minúsculas). #99
/// </summary>
public static class WebDomain
{
    public static string Normalize(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (s.Length == 0) return "";

        // Quitar el esquema (http://, https://, …).
        var scheme = s.IndexOf("://", System.StringComparison.Ordinal);
        if (scheme >= 0) s = s[(scheme + 3)..];

        // Quitar credenciales user@host.
        var at = s.IndexOf('@');
        if (at >= 0) s = s[(at + 1)..];

        // Quitar ruta / query / fragmento.
        var slash = s.IndexOfAny(['/', '?', '#']);
        if (slash >= 0) s = s[..slash];

        // Quitar puerto.
        var colon = s.IndexOf(':');
        if (colon >= 0) s = s[..colon];

        s = s.Trim().ToLowerInvariant();
        if (s.StartsWith("www.", System.StringComparison.Ordinal)) s = s[4..];
        return s;
    }
}
