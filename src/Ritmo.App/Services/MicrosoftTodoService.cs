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

/// <summary>Una lista de Microsoft To Do (id + nombre). #64</summary>
public sealed record MsTodoList(string Id, string Title);

/// <summary>Una tarea de Microsoft To Do. #64</summary>
public sealed record MsTodoTask(string Id, string Title, bool Done, string? Updated);

/// <summary>
/// Cliente OAuth 2.0 (PKCE) de Microsoft To Do vía Microsoft Graph (#64). Las apps de escritorio se
/// registran como CLIENTE PÚBLICO en Azure: el Client ID es público (embebido) y NO hay secreto de
/// cliente; PKCE + loopback bastan. El refresh token se guarda CIFRADO en el almacén de credenciales
/// de Windows; el access token vive en memoria.
/// </summary>
public static class MicrosoftTodoService
{
    private const int CallbackPort = 51778;
    public const string RedirectUri = "http://127.0.0.1:51778/";

    private const string VaultResource = "Ritmo.MicrosoftTodo";
    private const string VaultRefresh = "refresh_token";
    private const string GraphBase = "https://graph.microsoft.com/v1.0";

    // Client ID (Application ID) de la app registrada en Azure (PÚBLICO; va embebido como el de Google).
    // Pendiente: el usuario registra la app en portal.azure.com y pega aquí su Application (client) ID. #64
    public const string DefaultClientId = "";

    private static readonly HttpClient Http = new();
    private static string? _accessToken;
    private static DateTimeOffset _accessExpiry;

    /// <summary>Client ID a usar: el embebido (o un override en la config, si lo hubiera).</summary>
    public static string ClientId => AppState.Load().MicrosoftClientId is { Length: > 0 } id ? id : DefaultClientId;

    /// <summary>¿Está configurado el Client ID (app registrada)?</summary>
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>¿Hay un refresh token guardado (sesión iniciada)?</summary>
    public static bool HasSession => GetVault(VaultRefresh) is not null;

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

    /// <summary>Cierra sesión: borra el refresh token.</summary>
    public static void SignOut()
    {
        _accessToken = null; _accessExpiry = default;
        RemoveVault(VaultRefresh);
    }

    // ---------- Autorización (PKCE, cliente público) ----------

    /// <summary>
    /// Lanza el navegador para iniciar sesión en Microsoft, captura el callback en loopback y canjea el
    /// código por tokens (sin secreto de cliente). Persiste el refresh token. true si quedó autorizado.
    /// </summary>
    public static async Task<bool> AuthorizeAsync(CancellationToken ct = default)
    {
        var clientId = ClientId;
        if (string.IsNullOrWhiteSpace(clientId)) return false;

        var verifier = OAuthPkce.NewVerifier();
        var challenge = OAuthPkce.Challenge(verifier);
        var state = OAuthPkce.NewState();
        var url = MicrosoftAuth.AuthorizeUrl(clientId, RedirectUri, challenge, state);

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

            var q = OAuthPkce.ParseQuery(pathAndQuery);
            if (!q.TryGetValue("code", out var code) || q.GetValueOrDefault("state") != state) return false;
            return await ExchangeCodeAsync(code, verifier, clientId, ct);
        }
        catch { return false; }
        finally { listener.Stop(); }
    }

    private static async Task WriteResponseAsync(NetworkStream stream, CancellationToken ct)
    {
        const string html =
            "<!doctype html><meta charset=utf-8><title>Ritmo</title>" +
            "<body style='font-family:Segoe UI,sans-serif;background:#121212;color:#fff;text-align:center;padding-top:80px'>" +
            "<h2 style='color:#2564cf'>Conectado a Microsoft To Do</h2><p>Ya puedes volver a Ritmo.</p></body>";
        var body = Encoding.UTF8.GetBytes(html);
        var head = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(head, ct);
        await stream.WriteAsync(body, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<bool> ExchangeCodeAsync(string code, string verifier, string clientId, CancellationToken ct)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = verifier,
            ["scope"] = MicrosoftAuth.TasksScope
        };
        using var resp = await Http.PostAsync(MicrosoftAuth.TokenEndpoint, new FormUrlEncodedContent(fields), ct);
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
            ["client_id"] = clientId,
            ["scope"] = MicrosoftAuth.TasksScope
        };
        using var resp = await Http.PostAsync(MicrosoftAuth.TokenEndpoint, new FormUrlEncodedContent(fields), ct);
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

    // ---------- API de Microsoft Graph (To Do) ----------

    /// <summary>Lista las listas de To Do del usuario.</summary>
    public static async Task<IReadOnlyList<MsTodoList>> GetTaskListsAsync(CancellationToken ct = default)
    {
        if (!await EnsureAccessTokenAsync(ct)) throw new InvalidOperationException("No hay sesión de Microsoft.");
        var result = new List<MsTodoList>();
        string? url = $"{GraphBase}/me/todo/lists?$top=100";
        while (!string.IsNullOrEmpty(url))
        {
            var body = await SendAsync(HttpMethod.Get, url, null, ct, throwOnError: true);
            if (body is null) break;
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("value", out var items))
                foreach (var it in items.EnumerateArray())
                    result.Add(new MsTodoList(
                        it.GetProperty("id").GetString() ?? "",
                        it.TryGetProperty("displayName", out var n) ? n.GetString() ?? "(sin nombre)" : "(sin nombre)"));
            url = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl) ? nl.GetString() : null;
        }
        return result;
    }

    /// <summary>Lista las tareas (incluidas completadas) de una lista de To Do.</summary>
    public static async Task<IReadOnlyList<MsTodoTask>> ListTasksAsync(string listId, CancellationToken ct = default)
    {
        var result = new List<MsTodoTask>();
        string? url = $"{GraphBase}/me/todo/lists/{Uri.EscapeDataString(listId)}/tasks?$top=100";
        while (!string.IsNullOrEmpty(url) && result.Count < 1000)
        {
            var body = await SendAsync(HttpMethod.Get, url, null, ct);
            if (body is null) break;
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("value", out var items))
                foreach (var it in items.EnumerateArray())
                    result.Add(ParseTask(it));
            url = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl) ? nl.GetString() : null;
        }
        return result;
    }

    /// <summary>Crea una lista de To Do y devuelve su id (o null si falla).</summary>
    public static async Task<string?> InsertTaskListAsync(string title, CancellationToken ct = default)
    {
        var body = await SendAsync(HttpMethod.Post, $"{GraphBase}/me/todo/lists",
            JsonSerializer.Serialize(new { displayName = title }), ct);
        if (body is null) return null;
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    /// <summary>Crea una tarea en una lista y devuelve la tarea creada (id + lastModified).</summary>
    public static async Task<MsTodoTask?> InsertTaskAsync(string listId, string title, bool done, CancellationToken ct = default)
    {
        var body = await SendAsync(HttpMethod.Post, $"{GraphBase}/me/todo/lists/{Uri.EscapeDataString(listId)}/tasks",
            JsonSerializer.Serialize(new { title, status = done ? "completed" : "notStarted" }), ct);
        if (body is null) return null;
        using var doc = JsonDocument.Parse(body);
        return ParseTask(doc.RootElement);
    }

    /// <summary>Actualiza el título/estado de una tarea y devuelve la tarea (con lastModified nuevo).</summary>
    public static async Task<MsTodoTask?> PatchTaskAsync(string listId, string taskId, string title, bool done, CancellationToken ct = default)
    {
        var body = await SendAsync(HttpMethod.Patch,
            $"{GraphBase}/me/todo/lists/{Uri.EscapeDataString(listId)}/tasks/{Uri.EscapeDataString(taskId)}",
            JsonSerializer.Serialize(new { title, status = done ? "completed" : "notStarted" }), ct);
        if (body is null) return null;
        using var doc = JsonDocument.Parse(body);
        return ParseTask(doc.RootElement);
    }

    private static MsTodoTask ParseTask(JsonElement it)
    {
        string id = it.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
        string title = it.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        bool done = it.TryGetProperty("status", out var st) && st.GetString() == "completed";
        string? updated = it.TryGetProperty("lastModifiedDateTime", out var u) ? u.GetString() : null;
        return new MsTodoTask(id, title, done, updated);
    }

    /// <summary>Petición autenticada genérica; devuelve el cuerpo de la respuesta o null si falla.</summary>
    private static async Task<string?> SendAsync(HttpMethod method, string url, string? jsonBody, CancellationToken ct, bool throwOnError = false)
    {
        if (!await EnsureAccessTokenAsync(ct))
        {
            if (throwOnError) throw new InvalidOperationException("No hay sesión de Microsoft.");
            return null;
        }
        using var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        if (jsonBody is not null) req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            if (!throwOnError) return null;
            var b = await resp.Content.ReadAsStringAsync(ct);
            if (b.Length > 200) b = b[..200];
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode}. {b}");
        }
        return await resp.Content.ReadAsStringAsync(ct);
    }
}
