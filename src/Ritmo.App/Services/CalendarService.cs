using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ritmo.Core.Interop;
using Ritmo.Core.Model;

namespace Ritmo_App.Services;

/// <summary>
/// Descarga los calendarios suscritos (enlaces ICS) y devuelve sus eventos dentro
/// de un rango, parseados por <see cref="ICalendar.ImportEvents"/>. Solo lectura,
/// best-effort: un feed inaccesible se ignora. #112
/// </summary>
public static class CalendarService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static async Task<IReadOnlyList<CalendarEvent>> FetchAsync(
        IReadOnlyList<CalendarFeed> feeds, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var all = new List<CalendarEvent>();
        foreach (var feed in feeds)
        {
            try
            {
                // webcal:// es ICS por HTTP(S).
                var url = feed.Url.Replace("webcal://", "https://", StringComparison.OrdinalIgnoreCase);
                var ics = await Http.GetStringAsync(url, ct);
                all.AddRange(ICalendar.ImportEvents(ics, from, to, feed.Name));
            }
            catch { /* feed caído / sin red: ignorar */ }
        }
        return all.OrderBy(e => e.Start).ToList();
    }

    /// <summary>
    /// Todas las fuentes de eventos del overlay: suscripciones ICS (#112) + mis calendarios de Google
    /// por OAuth si está activado (#79). Best-effort: cada fuente se ignora si falla.
    /// </summary>
    public static async Task<IReadOnlyList<CalendarEvent>> FetchAllAsync(
        Ritmo.Core.Persistence.AppSettings settings, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var all = new List<CalendarEvent>();
        if (settings.CalendarFeeds.Count > 0)
            try { all.AddRange(await FetchAsync(settings.CalendarFeeds, from, to, ct)); } catch { }
        if (settings.ShowGoogleCalendar && GoogleTasksService.HasSession)
            try { all.AddRange(await GoogleCalendarService.FetchEventsAsync(from, to, settings.GoogleCalendarId, ct)); } catch { }
        return all.OrderBy(e => e.Start).ToList();
    }
}
