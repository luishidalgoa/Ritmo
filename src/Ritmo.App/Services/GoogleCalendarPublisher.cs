using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ritmo.Core.Model;
using Ritmo.Core.Sync;

namespace Ritmo_App.Services;

/// <summary>
/// Orquesta la publicación (una vía) del horario de Ritmo en Google Calendar (#112 Fase 2). Crea un
/// calendario dedicado "Ritmo", convierte el horario en eventos (Core <see cref="CalendarPublish"/>) y
/// los crea/actualiza/borra contra Google deduplicando por <see cref="CalendarLink"/>. Guarda el estado
/// (id del calendario + vínculos) UNA vez al final.
/// </summary>
internal static class GoogleCalendarPublisher
{
    public const string CalendarName = "Ritmo";

    public sealed record PublishResult(bool Ok, int Created, int Updated, int Deleted, string? Error);

    public static async Task<PublishResult> PublishAsync(CancellationToken ct = default)
    {
        if (await GoogleTasksService.GetAccessTokenAsync(ct) is null)
            return new PublishResult(false, 0, 0, 0, "No conectado a Google.");

        try
        {
            var s = AppState.Load();

            // 1. Asegurar el calendario dedicado.
            var calId = s.GoogleCalendarId;
            if (string.IsNullOrEmpty(calId) || !await GoogleCalendarService.CalendarExistsAsync(calId, ct))
            {
                calId = await GoogleCalendarService.CreateCalendarAsync(CalendarName, ct);
                if (calId is null) return new PublishResult(false, 0, 0, 0, "No se pudo crear el calendario en Google.");
            }

            var tz = GoogleCalendarService.IanaTimeZone();
            var specs = CalendarPublish.BuildSpecs(s.Plan, s.OneOffSessions);
            var byKey = s.CalendarPublishLinks.ToDictionary(l => l.Key, l => l.EventId);
            var specKeys = new HashSet<string>(specs.Select(x => x.Key));

            int created = 0, updated = 0, deleted = 0;
            var newLinks = new List<CalendarLink>();

            // 2. Crear/actualizar cada sesión.
            foreach (var spec in specs)
            {
                if (byKey.TryGetValue(spec.Key, out var eid))
                {
                    if (await GoogleCalendarService.UpdateEventAsync(calId, eid, spec, tz, ct))
                    { updated++; newLinks.Add(new CalendarLink { Key = spec.Key, EventId = eid }); continue; }
                    // el evento ya no existía → recrearlo
                }
                var newId = await GoogleCalendarService.InsertEventAsync(calId, spec, tz, ct);
                if (newId is not null) { created++; newLinks.Add(new CalendarLink { Key = spec.Key, EventId = newId }); }
            }

            // 3. Borrar los eventos cuya sesión ya no existe (huérfanos).
            foreach (var link in s.CalendarPublishLinks)
            {
                if (specKeys.Contains(link.Key)) continue;
                if (await GoogleCalendarService.DeleteEventAsync(calId, link.EventId, ct)) deleted++;
            }

            AppState.Store.Save(s with { GoogleCalendarId = calId, CalendarPublishLinks = newLinks });
            return new PublishResult(true, created, updated, deleted, null);
        }
        catch (Exception ex)
        {
            return new PublishResult(false, 0, 0, 0, ex.Message);
        }
    }

    /// <summary>Quita TODO lo publicado: borra el calendario "Ritmo" de Google y limpia el estado local.</summary>
    public static async Task<PublishResult> UnpublishAsync(CancellationToken ct = default)
    {
        if (await GoogleTasksService.GetAccessTokenAsync(ct) is null)
            return new PublishResult(false, 0, 0, 0, "No conectado a Google.");
        try
        {
            var s = AppState.Load();
            int removed = s.CalendarPublishLinks.Count;
            if (!string.IsNullOrEmpty(s.GoogleCalendarId))
                await GoogleCalendarService.DeleteCalendarAsync(s.GoogleCalendarId, ct);
            AppState.Store.Save(s with { GoogleCalendarId = null, CalendarPublishLinks = [] });
            return new PublishResult(true, 0, 0, removed, null);
        }
        catch (Exception ex)
        {
            return new PublishResult(false, 0, 0, 0, ex.Message);
        }
    }
}
