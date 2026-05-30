using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ritmo.Core.Focus;

namespace Ritmo_App.Services;

/// <summary>Playlist de Navidrome lista para pintar (con URL de carátula ya firmada). #107</summary>
public sealed record NavidromePlaylist(string Id, string Name, int SongCount, string Owner, string? CoverUrl);

/// <summary>
/// Cliente mínimo de Navidrome (API Subsonic): lista las playlists del usuario.
/// Auth por token (la contraseña no viaja en claro). La lógica de URLs/token vive
/// en <see cref="Subsonic"/>; aquí solo va la red. La contraseña NO se persiste. #107
/// </summary>
public static class NavidromeService
{
    private static readonly HttpClient Http = new();

    /// <summary>URL del servidor donde abrir la playlist en el navegador (web UI de Navidrome).</summary>
    public static string PlaylistWebUrl(string serverUrl, string playlistId)
        => $"{Subsonic.NormalizeServerUrl(serverUrl)}/app/#/playlist/{Uri.EscapeDataString(playlistId)}/show";

    public static async Task<IReadOnlyList<NavidromePlaylist>> GetPlaylistsAsync(
        string serverUrl, string user, string password, CancellationToken ct = default)
    {
        var salt = SaltHex();
        var token = Subsonic.Token(password, salt);
        var url = Subsonic.BuildUrl(serverUrl, "getPlaylists", user, token, salt);

        using var resp = await Http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode} {resp.StatusCode}.");

        var err = SubsonicError(body);
        if (err is not null) throw new InvalidOperationException(err);

        var result = new List<NavidromePlaylist>();
        foreach (var p in Subsonic.ParsePlaylists(body))
        {
            string? cover = p.CoverArt is null ? null
                : Subsonic.BuildUrl(serverUrl, "getCoverArt", user, token, salt,
                    extra: new[]
                    {
                        new KeyValuePair<string, string>("id", p.CoverArt),
                        new KeyValuePair<string, string>("size", "160")
                    });
            result.Add(new NavidromePlaylist(p.Id, p.Name, p.SongCount, p.Owner, cover));
        }
        return result;
    }

    private static string SaltHex() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();

    /// <summary>Si la respuesta Subsonic indica error, devuelve su mensaje; si no, null.</summary>
    private static string? SubsonicError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("subsonic-response", out var resp)) return "Respuesta no válida del servidor.";
            if (resp.TryGetProperty("status", out var st) && st.GetString() == "failed")
                return resp.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m)
                    ? m.GetString() ?? "El servidor rechazó la petición."
                    : "El servidor rechazó la petición (¿usuario o contraseña incorrectos?).";
            return null;
        }
        catch (JsonException) { return "El servidor no devolvió JSON Subsonic válido."; }
    }
}
