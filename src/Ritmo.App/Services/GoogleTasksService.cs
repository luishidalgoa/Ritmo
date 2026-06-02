using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ritmo.Core.Sync;

namespace Ritmo_App.Services;

/// <summary>Una lista de Google Tasks (id + título). #64</summary>
public sealed record GoogleTaskList(string Id, string Title);

/// <summary>Una tarea de Google Tasks. #64</summary>
public sealed record GoogleTask(string Id, string Title, bool Done, int Position, string? Notes, string? Updated);

/// <summary>
/// Cliente OAuth 2.0 (PKCE) de Google Tasks (#64). El Client ID (público) vive en la config;
/// el secreto de cliente (no confidencial para apps de escritorio) y el refresh token se guardan
/// CIFRADOS en el almacén de credenciales de Windows, nunca en el JSON ni en el repositorio.
/// El access token vive en memoria.
/// </summary>
public static class GoogleTasksService
{
    private const int CallbackPort = 51777;
    public const string RedirectUri = "http://127.0.0.1:51777/";

    private const string VaultResource = "Ritmo.GoogleTasks";
    private const string VaultRefresh = "refresh_token";
    private const string VaultSecret = "client_secret";
    private const string ApiBase = "https://tasks.googleapis.com/tasks/v1";

    // Client ID OAuth de la app Ritmo (PÚBLICO; los Client IDs no son secretos y van embebidos,
    // igual que el de Spotify). Así el usuario solo pulsa «Conectar», sin pegar credenciales. #64
    public const string DefaultClientId = "273778944021-i8e8nac1tjpqne3bem1q5smchepj249v.apps.googleusercontent.com";

    private static readonly HttpClient Http = new();
    private static string? _accessToken;
    private static DateTimeOffset _accessExpiry;

    /// <summary>Client ID a usar: el embebido por defecto (o un override en la config, si lo hubiera).</summary>
    public static string ClientId => AppState.Load().GoogleClientId is { Length: > 0 } id ? id : DefaultClientId;

    /// <summary>¿Hay un refresh token guardado (sesión iniciada)?</summary>
    public static bool HasSession => GetVault(VaultRefresh) is not null;

    /// <summary>¿Está guardado el secreto de cliente (necesario para el intercambio de tokens)?</summary>
    public static bool HasStoredSecret => GetVault(VaultSecret) is not null;

    /// <summary>Guarda el secreto de cliente en el almacén seguro (fallback de configuración única).</summary>
    public static void StoreClientSecret(string secret) => StoreVault(VaultSecret, secret ?? "");

    // ---------- Almacén seguro ----------

    private static string? GetVault(string user)
    {
        try
        {
            var vault = new Windows.Security.Credentials.PasswordVault();
            var cred = vault.Retrieve(VaultResource, user);
            cred.RetrievePassword();
            return string.IsNullOrEmpty(cred.Password) ? null : cred.Password;
        }
        catch { return null; }
    }

    private static void StoreVault(string user, string value)
    {
        try
        {
            var vault = new Windows.Security.Credentials.PasswordVault();
            try { vault.Remove(vault.Retrieve(VaultResource, user)); } catch { }
            vault.Add(new Windows.Security.Credentials.PasswordCredential(VaultResource, user, value));
        }
        catch { /* sin almacén: no persiste */ }
    }

    private static void RemoveVault(string user)
    {
        try { var vault = new Windows.Security.Credentials.PasswordVault(); vault.Remove(vault.Retrieve(VaultResource, user)); }
        catch { }
    }

    /// <summary>Cierra sesión: borra el refresh token (conserva el secreto para reconectar fácil).</summary>
    public static void SignOut()
    {
        _accessToken = null; _accessExpiry = default;
        RemoveVault(VaultRefresh);
    }

    // ---------- Autorización (PKCE) ----------

    /// <summary>
    /// Lanza el navegador para iniciar sesión en Google, captura el callback en loopback y canjea el
    /// código por tokens. Persiste el secreto y el refresh token. Devuelve true si quedó autorizado.
    /// </summary>
    public static async Task<bool> AuthorizeAsync(CancellationToken ct = default)
    {
        var clientId = ClientId;
        var clientSecret = GetVault(VaultSecret) ?? "";   // el usuario lo guardó una vez; embebido el Client ID
        if (string.IsNullOrWhiteSpace(clientId)) return false;

        var verifier = GoogleAuth.NewVerifier();
        var challenge = GoogleAuth.Challenge(verifier);
        var state = GoogleAuth.NewState();
        var url = GoogleAuth.AuthorizeUrl(clientId, RedirectUri, challenge, state);

        var listener = new TcpListener(IPAddress.Loopback, CallbackPort);
        listener.Start();
        try
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch { return false; }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));

            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = client.GetStream();
            var buffer = new byte[8192];
            int n = await stream.ReadAsync(buffer, timeout.Token);
            var requestLine = Encoding.ASCII.GetString(buffer, 0, n).Split('\n')[0];
            var pathAndQuery = requestLine.Split(' ').Length > 1 ? requestLine.Split(' ')[1] : "";
            await WriteResponseAsync(stream, timeout.Token);

            var q = GoogleAuth.ParseQuery(pathAndQuery);
            if (!q.TryGetValue("code", out var code) || q.GetValueOrDefault("state") != state) return false;
            return await ExchangeCodeAsync(code, verifier, clientId, clientSecret ?? "", ct);
        }
        catch { return false; }
        finally { listener.Stop(); }
    }

    private static async Task WriteResponseAsync(NetworkStream stream, CancellationToken ct)
    {
        const string html =
            "<!doctype html><meta charset=utf-8><title>Ritmo</title>" +
            "<body style='font-family:Segoe UI,sans-serif;background:#121212;color:#fff;text-align:center;padding-top:80px'>" +
            "<h2 style='color:#4285F4'>Conectado a Google Tasks</h2><p>Ya puedes volver a Ritmo.</p></body>";
        var body = Encoding.UTF8.GetBytes(html);
        var head = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(head, ct);
        await stream.WriteAsync(body, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<bool> ExchangeCodeAsync(string code, string verifier, string clientId, string clientSecret, CancellationToken ct)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = verifier
        };
        if (!string.IsNullOrEmpty(clientSecret)) fields["client_secret"] = clientSecret;
        using var resp = await Http.PostAsync(GoogleAuth.TokenEndpoint, new FormUrlEncodedContent(fields), ct);
        if (!resp.IsSuccessStatusCode) return false;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return ApplyTokenResponse(doc.RootElement);
    }

    private static async Task<bool> RefreshAsync(CancellationToken ct)
    {
        var refresh = GetVault(VaultRefresh);
        var clientId = ClientId;
        if (refresh is null || string.IsNullOrWhiteSpace(clientId)) return false;
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh,
            ["client_id"] = clientId!
        };
        var secret = GetVault(VaultSecret);
        if (!string.IsNullOrEmpty(secret)) fields["client_secret"] = secret;
        using var resp = await Http.PostAsync(GoogleAuth.TokenEndpoint, new FormUrlEncodedContent(fields), ct);
        if (!resp.IsSuccessStatusCode) return false;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return ApplyTokenResponse(doc.RootElement);
    }

    private static bool ApplyTokenResponse(JsonElement root)
    {
        if (!root.TryGetProperty("access_token", out var at)) return false;
        _accessToken = at.GetString();
        var expires = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
        _accessExpiry = DateTimeOffset.UtcNow.AddSeconds(expires - 60);
        if (root.TryGetProperty("refresh_token", out var rt) && rt.GetString() is { Length: > 0 } newRefresh)
            StoreVault(VaultRefresh, newRefresh);
        return _accessToken is not null;
    }

    private static async Task<bool> EnsureAccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _accessExpiry) return true;
        return await RefreshAsync(ct);
    }

    // ---------- API de Google Tasks ----------

    /// <summary>Lista las listas de tareas del usuario (para verificar la conexión y para el mapeo).</summary>
    public static async Task<IReadOnlyList<GoogleTaskList>> GetTaskListsAsync(CancellationToken ct = default)
    {
        if (!await EnsureAccessTokenAsync(ct)) throw new InvalidOperationException("No hay sesión de Google.");
        var result = new List<GoogleTaskList>();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/users/@me/lists?maxResults=100");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (body.Length > 200) body = body[..200];
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode}. {body}");
        }
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("items", out var items))
            foreach (var it in items.EnumerateArray())
                result.Add(new GoogleTaskList(
                    it.GetProperty("id").GetString() ?? "",
                    it.TryGetProperty("title", out var t) ? t.GetString() ?? "(sin nombre)" : "(sin nombre)"));
        return result;
    }

    /// <summary>Lista las tareas (incluidas completadas) de una lista de Google.</summary>
    public static async Task<IReadOnlyList<GoogleTask>> ListTasksAsync(string listId, CancellationToken ct = default)
    {
        var result = new List<GoogleTask>();
        string? pageToken = null;
        do
        {
            var url = $"{ApiBase}/lists/{Uri.EscapeDataString(listId)}/tasks?showCompleted=true&showHidden=true&maxResults=100";
            if (!string.IsNullOrEmpty(pageToken)) url += "&pageToken=" + Uri.EscapeDataString(pageToken);
            var body = await SendAsync(HttpMethod.Get, url, null, ct);
            if (body is null) break;
            using var doc = JsonDocument.Parse(body);
            pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var nt) ? nt.GetString() : null;
            if (doc.RootElement.TryGetProperty("items", out var items))
                foreach (var it in items.EnumerateArray())
                    result.Add(ParseTask(it));
        }
        while (!string.IsNullOrEmpty(pageToken) && result.Count < 1000);
        return result;
    }

    /// <summary>Crea una lista de Google Tasks y devuelve su id (o null si falla).</summary>
    public static async Task<string?> InsertTaskListAsync(string title, CancellationToken ct = default)
    {
        var body = await SendAsync(HttpMethod.Post, $"{ApiBase}/users/@me/lists",
            JsonSerializer.Serialize(new { title }), ct);
        if (body is null) return null;
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    /// <summary>Crea una tarea en una lista de Google y devuelve la tarea creada (con id + updated).</summary>
    public static async Task<GoogleTask?> InsertTaskAsync(string listId, string title, bool done, CancellationToken ct = default)
    {
        var body = await SendAsync(HttpMethod.Post, $"{ApiBase}/lists/{Uri.EscapeDataString(listId)}/tasks",
            JsonSerializer.Serialize(new { title, status = done ? "completed" : "needsAction" }), ct);
        if (body is null) return null;
        using var doc = JsonDocument.Parse(body);
        return ParseTask(doc.RootElement);
    }

    /// <summary>Actualiza el título/estado de una tarea de Google y devuelve la tarea (con updated nuevo).</summary>
    public static async Task<GoogleTask?> PatchTaskAsync(string listId, string taskId, string title, bool done, CancellationToken ct = default)
    {
        var body = await SendAsync(HttpMethod.Patch,
            $"{ApiBase}/lists/{Uri.EscapeDataString(listId)}/tasks/{Uri.EscapeDataString(taskId)}",
            JsonSerializer.Serialize(new { title, status = done ? "completed" : "needsAction" }), ct);
        if (body is null) return null;
        using var doc = JsonDocument.Parse(body);
        return ParseTask(doc.RootElement);
    }

    /// <summary>Borra una tarea de Google. true si se borró o ya no existía (404); false si falló.</summary>
    public static async Task<bool> DeleteTaskAsync(string listId, string taskId, CancellationToken ct = default)
    {
        if (!await EnsureAccessTokenAsync(ct)) return false;
        using var req = new HttpRequestMessage(HttpMethod.Delete,
            $"{ApiBase}/lists/{Uri.EscapeDataString(listId)}/tasks/{Uri.EscapeDataString(taskId)}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        using var resp = await Http.SendAsync(req, ct);
        return resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.NotFound;
    }

    private static GoogleTask ParseTask(JsonElement it)
    {
        string id = it.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
        string title = it.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        bool done = it.TryGetProperty("status", out var st) && st.GetString() == "completed";
        string? notes = it.TryGetProperty("notes", out var n) ? n.GetString() : null;
        string? updated = it.TryGetProperty("updated", out var u) ? u.GetString() : null;
        int pos = 0;
        if (it.TryGetProperty("position", out var p) && int.TryParse(p.GetString(), out var pp)) pos = pp;
        return new GoogleTask(id, title, done, pos, notes, updated);
    }

    /// <summary>Petición autenticada genérica; devuelve el cuerpo de la respuesta o null si falla.</summary>
    private static async Task<string?> SendAsync(HttpMethod method, string url, string? jsonBody, CancellationToken ct)
    {
        if (!await EnsureAccessTokenAsync(ct)) return null;
        using var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        if (jsonBody is not null) req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync(ct);
    }
}
