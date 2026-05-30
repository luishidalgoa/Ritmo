namespace Ritmo.Core.Focus;

/// <summary>
/// Helpers puros para construir el "target" de lanzamiento de apps de música. #98
/// </summary>
public static class MusicLaunch
{
    /// <summary>
    /// Convierte una playlist de Spotify (enlace https o URI <c>spotify:</c>) en el
    /// target que abre Spotify directamente en ella. Vacío → <c>spotify:</c> (solo
    /// abre Spotify). Si no se reconoce el formato, se devuelve tal cual.
    /// </summary>
    public static string SpotifyTarget(string? playlist)
    {
        var p = (playlist ?? "").Trim();
        if (p.Length == 0) return "spotify:";
        if (p.StartsWith("spotify:", System.StringComparison.OrdinalIgnoreCase)) return p;

        var uri = FromOpenSpotifyUrl(p);
        return uri ?? p;
    }

    /// <summary>
    /// <c>https://open.spotify.com/[intl-xx/]playlist/&lt;id&gt;?si=…</c> →
    /// <c>spotify:playlist:&lt;id&gt;</c> (también vale album/track). Null si no encaja.
    /// </summary>
    private static string? FromOpenSpotifyUrl(string url)
    {
        const string marker = "open.spotify.com/";
        var idx = url.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var rest = url[(idx + marker.Length)..];
        var cut = rest.IndexOfAny(['?', '#']);
        if (cut >= 0) rest = rest[..cut];

        var parts = rest.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        var id = parts[^1];
        var type = parts[^2];
        if (id.Length == 0 || type.Length == 0) return null;
        return $"spotify:{type.ToLowerInvariant()}:{id}";
    }
}
