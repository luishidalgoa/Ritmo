using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ritmo.Core.Sync;

namespace Ritmo_App.Services;

/// <summary>Una tarea de Recordatorios de Apple, vista por el sync (#64). Id = URL del recurso .ics.</summary>
public sealed record AppleTodo(string Url, string Title, bool Done, string Etag);

/// <summary>
/// Cliente de Recordatorios de Apple vía iCloud CalDAV (#64). Apple no tiene API REST: se accede por
/// CalDAV con el Apple ID + una CONTRASEÑA DE APLICACIÓN (appleid.apple.com), guardada CIFRADA en el
/// almacén de credenciales de Windows. Descubre principal → calendar-home → colecciones VTODO (listas
/// de Recordatorios) y opera sus VTODO. El parseo XML/iCal es puro (<see cref="CalDavXml"/>/<see cref="IcalTodo"/>).
/// </summary>
public static class AppleRemindersService
{
    private const string BaseUrl = "https://caldav.icloud.com";
    private const string VaultResource = "Ritmo.AppleReminders";
    private const string VaultPassword = "app_password";

    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = true });

    /// <summary>Apple ID conectado (email), para mostrar; null = sin conectar.</summary>
    public static string? AppleId => AppState.Load().AppleId is { Length: > 0 } id ? id : null;

    /// <summary>¿Hay conexión (Apple ID + contraseña de app guardados)?</summary>
    public static bool HasSession => AppleId is not null && GetVault(VaultPassword) is not null;

    // ---------- Almacén seguro + conexión ----------

    private static string? GetVault(string user)
    {
        try
        {
            var cred = new Windows.Security.Credentials.PasswordVault().Retrieve(VaultResource, user);
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
        catch { }
    }

    private static void RemoveVault(string user)
    {
        try { var v = new Windows.Security.Credentials.PasswordVault(); v.Remove(v.Retrieve(VaultResource, user)); }
        catch { }
    }

    /// <summary>Guarda la conexión (Apple ID en la config, contraseña de app cifrada en el almacén).</summary>
    public static void Connect(string appleId, string appPassword)
    {
        var s = AppState.Load();
        AppState.Store.Save(s with { AppleId = appleId.Trim() });
        StoreVault(VaultPassword, appPassword);
    }

    /// <summary>Desconecta: borra el Apple ID de la config y la contraseña del almacén.</summary>
    public static void SignOut()
    {
        var s = AppState.Load();
        AppState.Store.Save(s with { AppleId = null });
        RemoveVault(VaultPassword);
    }

    private static AuthenticationHeaderValue? AuthHeader()
    {
        var id = AppleId; var pwd = GetVault(VaultPassword);
        if (id is null || pwd is null) return null;
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{pwd}"));
        return new AuthenticationHeaderValue("Basic", token);
    }

    // ---------- CalDAV ----------

    private static async Task<(int Status, string Body, string? Etag)> SendAsync(
        string method, string url, string? body, int depth, CancellationToken ct,
        string? ifNoneMatch = null, string contentType = "application/xml; charset=utf-8")
    {
        var auth = AuthHeader() ?? throw new InvalidOperationException("No hay conexión con iCloud.");
        using var req = new HttpRequestMessage(new HttpMethod(method), url);
        req.Headers.Authorization = auth;
        if (depth >= 0) req.Headers.TryAddWithoutValidation("Depth", depth.ToString());
        if (ifNoneMatch is not null) req.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        if (body is not null) req.Content = new StringContent(body, Encoding.UTF8);
        if (req.Content is not null) req.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        using var resp = await Http.SendAsync(req, ct);
        var text = resp.Content is null ? "" : await resp.Content.ReadAsStringAsync(ct);
        string? etag = resp.Headers.ETag?.Tag ?? (resp.Content?.Headers.TryGetValues("ETag", out var ev) == true ? ev.FirstOrDefault() : null);
        return ((int)resp.StatusCode, text, etag);
    }

    private static string Resolve(string baseUrl, string href)
    {
        if (string.IsNullOrEmpty(href)) return baseUrl;
        return Uri.TryCreate(new Uri(baseUrl), href, out var abs) ? abs.ToString() : href;
    }

    private const string PropfindPrincipal =
        "<d:propfind xmlns:d=\"DAV:\"><d:prop><d:current-user-principal/></d:prop></d:propfind>";
    private const string PropfindHome =
        "<d:propfind xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\"><d:prop><c:calendar-home-set/></d:prop></d:propfind>";
    private const string PropfindCollections =
        "<d:propfind xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\"><d:prop><d:displayname/><d:resourcetype/><c:supported-calendar-component-set/></d:prop></d:propfind>";
    private const string ReportVTodos =
        "<c:calendar-query xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\">" +
        "<d:prop><d:getetag/><c:calendar-data/></d:prop>" +
        "<c:filter><c:comp-filter name=\"VCALENDAR\"><c:comp-filter name=\"VTODO\"/></c:comp-filter></c:filter></c:calendar-query>";

    /// <summary>Verifica la conexión y devuelve el nº de listas de Recordatorios (para el estado).</summary>
    public static async Task<int> VerifyAsync(CancellationToken ct = default)
        => (await GetReminderListsAsync(ct)).Count;

    /// <summary>Descubre las colecciones CalDAV que soportan VTODO (las listas de Recordatorios).</summary>
    public static async Task<IReadOnlyList<(string Url, string Title)>> GetReminderListsAsync(CancellationToken ct = default)
    {
        // 1) principal
        var (st1, b1, _) = await SendAsync("PROPFIND", BaseUrl, PropfindPrincipal, 0, ct);
        if (st1 is < 200 or >= 300) throw new HttpRequestException($"iCloud PROPFIND principal: HTTP {st1}.");
        var principal = CalDavXml.CurrentUserPrincipal(b1);
        if (principal is null) throw new HttpRequestException("No se encontró el principal de iCloud (¿credenciales correctas?).");
        var principalUrl = Resolve(BaseUrl, principal);

        // 2) calendar-home-set
        var (_, b2, _) = await SendAsync("PROPFIND", principalUrl, PropfindHome, 0, ct);
        var home = CalDavXml.CalendarHomeSet(b2);
        if (home is null) throw new HttpRequestException("No se encontró el calendar-home de iCloud.");
        var homeUrl = Resolve(principalUrl, home);

        // 3) colecciones bajo el home; quedarse con las que soportan VTODO
        var (_, b3, _) = await SendAsync("PROPFIND", homeUrl, PropfindCollections, 1, ct);
        var result = new List<(string, string)>();
        // Colecciones especiales de iCloud que NO son listas (no admiten REPORT calendar-query).
        var special = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "outbox", "inbox", "notification", "dropbox" };
        foreach (var r in CalDavXml.ParseMultistatus(b3))
        {
            // Lista real = colección de tipo <calendar> que soporta VTODO. Descarta outbox/inbox/etc.
            if (!r.SupportsVTodo || !r.IsCalendar) continue;
            var url = Resolve(homeUrl, r.Href);
            if (url.TrimEnd('/').Equals(homeUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) continue;  // el propio home
            var lastSeg = url.TrimEnd('/');
            lastSeg = lastSeg[(lastSeg.LastIndexOf('/') + 1)..];
            if (special.Contains(lastSeg)) continue;
            result.Add((url, string.IsNullOrWhiteSpace(r.DisplayName) ? "Recordatorios" : r.DisplayName!));
        }
        return result;
    }

    /// <summary>
    /// Diagnóstico (#64): devuelve, en texto, TODAS las colecciones que iCloud expone bajo el home
    /// (sin filtrar), con sus flags. Sirve para ver por qué no aparecen las listas reales del usuario.
    /// </summary>
    public static async Task<string> DiagnoseAsync(CancellationToken ct = default)
    {
        var (st1, b1, _) = await SendAsync("PROPFIND", BaseUrl, PropfindPrincipal, 0, ct);
        if (st1 is < 200 or >= 300) return $"PROPFIND principal: HTTP {st1}.";
        var principal = CalDavXml.CurrentUserPrincipal(b1);
        if (principal is null) return "No se encontró el principal (¿credenciales?).";
        var principalUrl = Resolve(BaseUrl, principal);

        var (_, b2, _) = await SendAsync("PROPFIND", principalUrl, PropfindHome, 0, ct);
        var home = CalDavXml.CalendarHomeSet(b2);
        if (home is null) return $"Principal: {principalUrl}\nNo se encontró el calendar-home.";
        var homeUrl = Resolve(principalUrl, home);

        var (_, b3, _) = await SendAsync("PROPFIND", homeUrl, PropfindCollections, 1, ct);
        var cols = CalDavXml.ParseMultistatus(b3);
        var sb = new StringBuilder();
        sb.Append("Home: ").Append(homeUrl).Append('\n');
        sb.Append("Colecciones encontradas: ").Append(cols.Count).Append('\n');
        foreach (var r in cols)
        {
            var seg = r.Href.TrimEnd('/');
            seg = seg.Length == 0 ? "/" : seg[(seg.LastIndexOf('/') + 1)..];
            sb.Append(" · ").Append(seg)
              .Append("  nombre=\"").Append(r.DisplayName ?? "(sin nombre)").Append('"')
              .Append("  calendar=").Append(r.IsCalendar)
              .Append("  vtodo=").Append(r.SupportsVTodo)
              .Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Lista los VTODO de una colección (Recordatorios) con su etag.</summary>
    public static async Task<IReadOnlyList<AppleTodo>> ListTodosAsync(string collectionUrl, CancellationToken ct = default)
    {
        var (st, body, _) = await SendAsync("REPORT", collectionUrl, ReportVTodos, 1, ct);
        if (st is < 200 or >= 300)
        {
            var u = collectionUrl.Length > 80 ? "…" + collectionUrl[^80..] : collectionUrl;
            throw new HttpRequestException($"REPORT HTTP {st} en {u}");
        }
        var todos = new List<AppleTodo>();
        foreach (var r in CalDavXml.ParseMultistatus(body))
        {
            if (string.IsNullOrEmpty(r.CalendarData)) continue;
            var parsed = IcalTodo.Parse(r.CalendarData);
            if (parsed is null) continue;
            var url = Resolve(collectionUrl, r.Href);
            todos.Add(new AppleTodo(url, parsed.Value.Summary, parsed.Value.Done, r.Etag ?? parsed.Value.LastModified ?? ""));
        }
        return todos;
    }

    /// <summary>Crea un VTODO en una colección; devuelve (url del recurso, etag) o null.</summary>
    public static async Task<(string Url, string Etag)?> CreateTodoAsync(string collectionUrl, string text, bool done, CancellationToken ct = default)
    {
        var uid = $"ritmo-{Guid.NewGuid():N}";
        var ics = IcalTodo.Build(uid, text, done, DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ"));
        var url = collectionUrl.TrimEnd('/') + "/" + uid + ".ics";
        var (st, _, etag) = await SendAsync("PUT", url, ics, -1, ct, ifNoneMatch: "*", contentType: "text/calendar; charset=utf-8");
        if (st is < 200 or >= 300) return null;
        return (url, etag ?? await FetchEtagAsync(url, ct));
    }

    /// <summary>Actualiza un VTODO (PUT al mismo recurso); devuelve el nuevo etag o null.</summary>
    public static async Task<string?> UpdateTodoAsync(string resourceUrl, string text, bool done, CancellationToken ct = default)
    {
        var uid = UidFromUrl(resourceUrl);
        var ics = IcalTodo.Build(uid, text, done, DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ"));
        var (st, _, etag) = await SendAsync("PUT", resourceUrl, ics, -1, ct, contentType: "text/calendar; charset=utf-8");
        if (st is < 200 or >= 300) return null;
        return etag ?? await FetchEtagAsync(resourceUrl, ct);
    }

    /// <summary>Borra un VTODO (DELETE al recurso). true si se borró o ya no existía (404); false si falló.</summary>
    public static async Task<bool> DeleteTodoAsync(string resourceUrl, CancellationToken ct = default)
    {
        var (st, _, _) = await SendAsync("DELETE", resourceUrl, null, -1, ct);
        return st is (>= 200 and < 300) or 404;
    }

    private static async Task<string> FetchEtagAsync(string url, CancellationToken ct)
    {
        try
        {
            var (_, body, _) = await SendAsync("PROPFIND", url,
                "<d:propfind xmlns:d=\"DAV:\"><d:prop><d:getetag/></d:prop></d:propfind>", 0, ct);
            return CalDavXml.ParseMultistatus(body).FirstOrDefault()?.Etag ?? "";
        }
        catch { return ""; }
    }

    private static string UidFromUrl(string url)
    {
        var last = url.TrimEnd('/');
        int slash = last.LastIndexOf('/');
        var file = slash >= 0 ? last[(slash + 1)..] : last;
        return file.EndsWith(".ics", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
    }
}
