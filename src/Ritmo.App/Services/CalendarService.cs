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
}
