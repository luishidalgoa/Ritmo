using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Ritmo.Core.Interop;
using Ritmo.Core.Sync;

namespace Ritmo_App.Services;

/// <summary>
/// Cliente de Google Calendar para PUBLICAR el horario de Ritmo (#112 Fase 2). Reutiliza el OAuth de
/// <see cref="GoogleTasksService"/> (una sola conexión de Google cubre Tasks + Calendar); aquí solo se
/// hacen llamadas a la API v3: crear el calendario dedicado y crear/actualizar/borrar eventos. La
/// conversión horario→evento (recurrencia semanal, único) viene del Core (<see cref="CalendarEventSpec"/>).
/// </summary>
public static class GoogleCalendarService
{
    private const string ApiBase = "https://www.googleapis.com/calendar/v3";
    private static readonly HttpClient Http = new();

    /// <summary>Zona horaria IANA del equipo (Google la exige); fallback a UTC.</summary>
    public static string IanaTimeZone()
    {
        var tz = TimeZoneInfo.Local;
        if (tz.HasIanaId) return tz.Id;
        return TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out var iana) ? iana : "UTC";
    }

    private static async Task<(int Status, string Body)> SendAsync(HttpMethod method, string url, string? json, CancellationToken ct)
    {
        var token = await GoogleTasksService.GetAccessTokenAsync(ct);
        if (token is null) return (401, "");
        using var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        if (json is not null) req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        var body = resp.Content is null ? "" : await resp.Content.ReadAsStringAsync(ct);
        return ((int)resp.StatusCode, body);
    }

    /// <summary>Crea un calendario secundario y devuelve su id (o null si falla). #112</summary>
    public static async Task<string?> CreateCalendarAsync(string name, CancellationToken ct = default)
    {
        var (st, body) = await SendAsync(HttpMethod.Post, $"{ApiBase}/calendars",
            JsonSerializer.Serialize(new { summary = name }), ct);
        if (st is < 200 or >= 300) return null;
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    /// <summary>¿Existe (y es accesible) el calendario con ese id?</summary>
    public static async Task<bool> CalendarExistsAsync(string calendarId, CancellationToken ct = default)
    {
        var (st, _) = await SendAsync(HttpMethod.Get, $"{ApiBase}/calendars/{Uri.EscapeDataString(calendarId)}", null, ct);
        return st is >= 200 and < 300;
    }

    /// <summary>Borra el calendario entero (y todos sus eventos). true si OK o ya no existía.</summary>
    public static async Task<bool> DeleteCalendarAsync(string calendarId, CancellationToken ct = default)
    {
        var (st, _) = await SendAsync(HttpMethod.Delete, $"{ApiBase}/calendars/{Uri.EscapeDataString(calendarId)}", null, ct);
        return st is (>= 200 and < 300) or 404 or 410;
    }

    /// <summary>Crea un evento a partir del spec; devuelve su id (o null si falla).</summary>
    public static async Task<string?> InsertEventAsync(string calendarId, CalendarEventSpec spec, string tz, CancellationToken ct = default)
    {
        var (st, body) = await SendAsync(HttpMethod.Post,
            $"{ApiBase}/calendars/{Uri.EscapeDataString(calendarId)}/events", EventJson(spec, tz), ct);
        if (st is < 200 or >= 300) return null;
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    /// <summary>Actualiza un evento existente (PUT). true si OK; false si ya no existe o falla.</summary>
    public static async Task<bool> UpdateEventAsync(string calendarId, string eventId, CalendarEventSpec spec, string tz, CancellationToken ct = default)
    {
        var (st, _) = await SendAsync(HttpMethod.Put,
            $"{ApiBase}/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}", EventJson(spec, tz), ct);
        return st is >= 200 and < 300;
    }

    /// <summary>Borra un evento. true si se borró o ya no existía (404/410); false si falló.</summary>
    public static async Task<bool> DeleteEventAsync(string calendarId, string eventId, CancellationToken ct = default)
    {
        var (st, _) = await SendAsync(HttpMethod.Delete,
            $"{ApiBase}/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}", null, ct);
        return st is (>= 200 and < 300) or 404 or 410;
    }

    // ---------- LECTURA: eventos de mis calendarios para el overlay (#79) ----------

    /// <summary>
    /// Trae los eventos de TODOS mis calendarios de Google (vía OAuth) en [from, to], excluyendo
    /// <paramref name="excludeCalendarId"/> (el calendario "Ritmo", para no ver mis propias sesiones
    /// publicadas). Devuelve <see cref="CalendarEvent"/> para reusar el overlay del ICS. #79
    /// </summary>
    public static async Task<IReadOnlyList<CalendarEvent>> FetchEventsAsync(
        DateOnly from, DateOnly to, string? excludeCalendarId, CancellationToken ct = default)
    {
        var result = new List<CalendarEvent>();
        if (await GoogleTasksService.GetAccessTokenAsync(ct) is null) return result;

        var (cs, cbody) = await SendAsync(HttpMethod.Get, $"{ApiBase}/users/me/calendarList?maxResults=250&minAccessRole=reader", null, ct);
        if (cs is < 200 or >= 300) return result;
        var cals = new List<(string Id, string Summary)>();
        using (var cdoc = JsonDocument.Parse(cbody))
            if (cdoc.RootElement.TryGetProperty("items", out var items))
                foreach (var it in items.EnumerateArray())
                {
                    var id = it.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(id) || id == excludeCalendarId) continue;
                    var sum = it.TryGetProperty("summaryOverride", out var so) ? so.GetString()
                              : it.TryGetProperty("summary", out var sm) ? sm.GetString() : null;
                    cals.Add((id, string.IsNullOrWhiteSpace(sum) ? id : sum!));
                }

        var timeMin = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue)).ToString("o");
        var timeMax = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue)).ToString("o");

        foreach (var (id, sum) in cals)
        {
            var url = $"{ApiBase}/calendars/{Uri.EscapeDataString(id)}/events?singleEvents=true&orderBy=startTime&maxResults=250" +
                      $"&timeMin={Uri.EscapeDataString(timeMin)}&timeMax={Uri.EscapeDataString(timeMax)}";
            var (es, ebody) = await SendAsync(HttpMethod.Get, url, null, ct);
            if (es is < 200 or >= 300) continue;
            using var edoc = JsonDocument.Parse(ebody);
            if (!edoc.RootElement.TryGetProperty("items", out var evs)) continue;
            foreach (var ev in evs.EnumerateArray())
                if (ParseEvent(ev, sum) is { } ce) result.Add(ce);
        }
        return result;
    }

    private static CalendarEvent? ParseEvent(JsonElement ev, string calName)
    {
        if (ev.TryGetProperty("status", out var stt) && stt.GetString() == "cancelled") return null;
        var title = ev.TryGetProperty("summary", out var s) ? s.GetString() ?? "(sin título)" : "(sin título)";
        if (!ev.TryGetProperty("start", out var st) || !ev.TryGetProperty("end", out var en)) return null;
        var (start, allDay) = ParseWhen(st);
        var (end, _) = ParseWhen(en);
        if (start is null || end is null) return null;
        return new CalendarEvent(title, start.Value, end.Value, allDay, calName);
    }

    private static (DateTime? Dt, bool AllDay) ParseWhen(JsonElement when)
    {
        if (when.TryGetProperty("dateTime", out var dt) && dt.GetString() is { } s
            && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            return (dto.LocalDateTime, false);
        if (when.TryGetProperty("date", out var d) && d.GetString() is { } ds
            && DateTime.TryParse(ds, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return (date, true);
        return (null, false);
    }

    // ---------- Construcción del cuerpo del evento ----------

    private static string EventJson(CalendarEventSpec spec, string tz)
    {
        var body = new Dictionary<string, object>
        {
            ["summary"] = spec.Title,
            ["start"] = new { dateTime = spec.Start.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = tz },
            ["end"] = new { dateTime = spec.End.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = tz },
            ["extendedProperties"] = new { @private = new Dictionary<string, string> { ["ritmoKey"] = spec.Key } }
        };
        if (spec.WeeklyOn is DayOfWeek day)
            body["recurrence"] = new[] { BuildRRule(day, spec.Until) };
        return JsonSerializer.Serialize(body);
    }

    private static string BuildRRule(DayOfWeek day, DateOnly? until)
    {
        var rule = $"RRULE:FREQ=WEEKLY;BYDAY={ByDayCode(day)}";
        if (until is DateOnly u)
        {
            var lastLocal = DateTime.SpecifyKind(u.ToDateTime(new TimeOnly(23, 59, 59)), DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(lastLocal, TimeZoneInfo.Local);
            rule += ";UNTIL=" + utc.ToString("yyyyMMddTHHmmssZ");
        }
        return rule;
    }

    private static string ByDayCode(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "MO",
        DayOfWeek.Tuesday => "TU",
        DayOfWeek.Wednesday => "WE",
        DayOfWeek.Thursday => "TH",
        DayOfWeek.Friday => "FR",
        DayOfWeek.Saturday => "SA",
        _ => "SU"
    };
}
