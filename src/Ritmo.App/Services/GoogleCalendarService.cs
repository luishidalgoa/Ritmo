using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
