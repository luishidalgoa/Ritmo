using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ritmo.Core.Focus;

namespace Ritmo_App.Services;

/// <summary>Una playlist de Spotify para pintar en el modal. #106</summary>
public sealed record SpotifyPlaylist(string Id, string Uri, string Name, string? ImageUrl, int TrackCount, string Owner);

/// <summary>
/// Login OAuth 2.0 (PKCE) contra Spotify + lectura de las playlists del usuario.
/// El access token vive en memoria; el refresh token se guarda cifrado en el
/// almacén de credenciales de Windows (NO en el JSON exportable). #106
/// </summary>
public static class SpotifyService
{
    // Client ID público (flujo PKCE, sin secreto) de la app registrada en Spotify.
    public const string ClientId = "c507b246e1c846399d1c780020c16a58";
    public const string RedirectUri = "http://127.0.0.1:43117/callback";
    private const int CallbackPort = 43117;

    private const string VaultResource = "Ritmo.Spotify";
    private const string VaultUser = "refresh_token";

    private static readonly HttpClient Http = new();
    private static string? _accessToken;
    private static DateTimeOffset _accessExpiry;

    /// <summary>¿Hay una sesión guardada (refresh token) de Spotify?</summary>
    public static bool HasSession => GetStoredRefreshToken() is not null;

    // ---------- Almacén seguro (Credential Locker de Windows) ----------

    private static string? GetStoredRefreshToken()
    {
        try
        {
            var vault = new Windows.Security.Credentials.PasswordVault();
            var cred = vault.Retrieve(VaultResource, VaultUser);
            cred.RetrievePassword();
            return string.IsNullOrEmpty(cred.Password) ? null : cred.Password;
        }
        catch { return null; }   // no hay credencial guardada
    }

    private static void StoreRefreshToken(string token)
    {
        try
        {
            var vault = new Windows.Security.Credentials.PasswordVault();
            try { vault.Remove(vault.Retrieve(VaultResource, VaultUser)); } catch { }
            vault.Add(new Windows.Security.Credentials.PasswordCredential(VaultResource, VaultUser, token));
        }
        catch { /* sin almacén disponible: la sesión no persistirá */ }
    }

    /// <summary>Cierra sesión: borra el refresh token y el access token en memoria.</summary>
    public static void SignOut()
    {
        _accessToken = null;
        _accessExpiry = default;
        try
        {
            var vault = new Windows.Security.Credentials.PasswordVault();
            vault.Remove(vault.Retrieve(VaultResource, VaultUser));
        }
        catch { }
    }

    // ---------- Flujo de autorización (PKCE) ----------

    /// <summary>
    /// Lanza el navegador para que el usuario inicie sesión, captura el callback en
    /// el loopback y canjea el código por tokens. Devuelve true si quedó autorizado.
    /// </summary>
    public static async Task<bool> AuthorizeAsync(CancellationToken ct = default)
    {
        var verifier = SpotifyAuth.NewVerifier();
        var challenge = SpotifyAuth.Challenge(verifier);
        var state = SpotifyAuth.NewState();
        var url = SpotifyAuth.AuthorizeUrl(ClientId, RedirectUri, challenge, state);

        var listener = new TcpListener(IPAddress.Loopback, CallbackPort);
        listener.Start();
        try
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch { listener.Stop(); return false; }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));

            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = client.GetStream();

            var buffer = new byte[8192];
            int n = await stream.ReadAsync(buffer, timeout.Token);
            var requestLine = Encoding.ASCII.GetString(buffer, 0, n).Split('\n')[0];
            var pathAndQuery = requestLine.Split(' ').Length > 1 ? requestLine.Split(' ')[1] : "";

            await WriteResponseAsync(stream, timeout.Token);

            var q = SpotifyAuth.ParseQuery(pathAndQuery);
            if (!q.TryGetValue("code", out var code) || q.GetValueOrDefault("state") != state)
                return false;

            return await ExchangeCodeAsync(code, verifier, ct);
        }
        catch { return false; }
        finally { listener.Stop(); }
    }

    private static async Task WriteResponseAsync(NetworkStream stream, CancellationToken ct)
    {
        const string html =
            "<!doctype html><meta charset=utf-8><title>Ritmo</title>" +
            "<body style='font-family:Segoe UI,sans-serif;background:#121212;color:#fff;text-align:center;padding-top:80px'>" +
            "<h2 style='color:#1DB954'>Conectado a Spotify</h2><p>Ya puedes volver a Ritmo.</p></body>";
        var body = Encoding.UTF8.GetBytes(html);
        var head = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(head, ct);
        await stream.WriteAsync(body, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<bool> ExchangeCodeAsync(string code, string verifier, CancellationToken ct)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = ClientId,
            ["code_verifier"] = verifier
        });
        using var resp = await Http.PostAsync(SpotifyAuth.TokenEndpoint, form, ct);
        if (!resp.IsSuccessStatusCode) return false;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return ApplyTokenResponse(doc.RootElement);
    }

    private static async Task<bool> RefreshAsync(CancellationToken ct)
    {
        var refresh = GetStoredRefreshToken();
        if (refresh is null) return false;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh,
            ["client_id"] = ClientId
        });
        using var resp = await Http.PostAsync(SpotifyAuth.TokenEndpoint, form, ct);
        if (!resp.IsSuccessStatusCode) return false;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return ApplyTokenResponse(doc.RootElement);
    }

    private static bool ApplyTokenResponse(JsonElement root)
    {
        if (!root.TryGetProperty("access_token", out var at)) return false;
        _accessToken = at.GetString();
        var expires = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
        _accessExpiry = DateTimeOffset.UtcNow.AddSeconds(expires - 60);   // margen
        if (root.TryGetProperty("refresh_token", out var rt) && rt.GetString() is { Length: > 0 } newRefresh)
            StoreRefreshToken(newRefresh);
        return _accessToken is not null;
    }

    private static async Task<bool> EnsureAccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _accessExpiry) return true;
        return await RefreshAsync(ct);
    }

    // ---------- Web API ----------

    /// <summary>Lee las playlists del usuario (paginadas). Lanza si no hay sesión válida.</summary>
    public static async Task<IReadOnlyList<SpotifyPlaylist>> GetPlaylistsAsync(CancellationToken ct = default)
    {
        if (!await EnsureAccessTokenAsync(ct))
            throw new InvalidOperationException("No hay sesión de Spotify.");

        var result = new List<SpotifyPlaylist>();
        var url = SpotifyAuth.PlaylistsEndpoint + "?limit=50";
        while (!string.IsNullOrEmpty(url) && result.Count < 200)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            using var resp = await Http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) break;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (root.TryGetProperty("items", out var items))
                foreach (var it in items.EnumerateArray())
                    result.Add(Parse(it));

            url = root.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String ? next.GetString()! : null;
        }
        return result;
    }

    private static SpotifyPlaylist Parse(JsonElement it)
    {
        string id = it.GetProperty("id").GetString() ?? "";
        string uri = it.TryGetProperty("uri", out var u) ? u.GetString() ?? $"spotify:playlist:{id}" : $"spotify:playlist:{id}";
        string name = it.TryGetProperty("name", out var nm) ? nm.GetString() ?? "(sin nombre)" : "(sin nombre)";
        string? image = null;
        if (it.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array && imgs.GetArrayLength() > 0)
            image = imgs[0].TryGetProperty("url", out var iu) ? iu.GetString() : null;
        int tracks = it.TryGetProperty("tracks", out var tr) && tr.TryGetProperty("total", out var tt) ? tt.GetInt32() : 0;
        string owner = it.TryGetProperty("owner", out var ow) && ow.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? "" : "";
        return new SpotifyPlaylist(id, uri, name, image, tracks, owner);
    }
}
