using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Notifications;

/// <summary>
/// Contenido de notificación listo para que el host lo muestre como toast del SO.
/// Es agnóstico del SO: solo texto + una etiqueta de deduplicación.
/// </summary>
public sealed record NotificationMessage
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    /// <summary>
    /// Etiqueta única por ocurrencia. El host la usa como Tag del toast para que
    /// re-disparar el mismo evento reemplace el aviso en lugar de apilarlo.
    /// </summary>
    public required string Tag { get; init; }
}

/// <summary>
/// Traduce un <see cref="PlannedEvent"/> (aviso previo / inicio de sesión) en el
/// texto que verá el usuario. PURO y testable: aquí se decide QUÉ se dice; el host
/// (Ritmo.App) solo se encarga de pintarlo con la API de toasts de Windows.
/// </summary>
public static class NotificationBuilder
{
    /// <summary>
    /// Construye el mensaje para un evento planificado. <paramref name="categoryName"/> es
    /// el nombre visible de la categoría del bloque (el host lo resuelve desde
    /// <c>AppSettings.Categories</c>); si es null se usa el id de la categoría. #83
    /// </summary>
    public static NotificationMessage ForEvent(PlannedEvent ev, string? categoryName = null)
    {
        var kind = string.IsNullOrWhiteSpace(categoryName) ? ev.Session.CategoryId : categoryName!;
        var title = string.IsNullOrWhiteSpace(ev.Session.Title) ? kind : ev.Session.Title.Trim();
        var hhmm = ev.SessionStartAt.ToString("HH:mm");

        return ev.Type switch
        {
            PlannedEventType.PreAlert => new NotificationMessage
            {
                Title = PreAlertHeadline(ev.MinutesBefore ?? 0),
                Body = $"{kind} · {title} · a las {hhmm}",
                Tag = TagFor(ev)
            },
            PlannedEventType.SessionStart => new NotificationMessage
            {
                Title = "Es la hora de concentrarte",
                Body = $"{kind} · {title} · {hhmm}",
                Tag = TagFor(ev)
            },
            _ => new NotificationMessage
            {
                Title = title,
                Body = $"{kind} · {hhmm}",
                Tag = TagFor(ev)
            }
        };
    }

    /// <summary>Titular del aviso previo según los minutos que faltan.</summary>
    public static string PreAlertHeadline(int minutesBefore)
    {
        if (minutesBefore <= 0) return "Tu sesión está por empezar";
        if (minutesBefore >= 60 && minutesBefore % 60 == 0)
        {
            var h = minutesBefore / 60;
            return h == 1 ? "Tu sesión empieza en 1 hora" : $"Tu sesión empieza en {h} horas";
        }
        return minutesBefore == 1
            ? "Tu sesión empieza en 1 minuto"
            : $"Tu sesión empieza en {minutesBefore} minutos";
    }

    /// <summary>Etiqueta de deduplicación: única por tipo + ocurrencia + minutos.</summary>
    public static string TagFor(PlannedEvent ev)
    {
        var when = ev.SessionStartAt.ToString("yyyyMMddHHmm");
        return ev.Type == PlannedEventType.PreAlert
            ? $"prealert-{when}-{ev.MinutesBefore ?? 0}"
            : $"start-{when}";
    }
}
