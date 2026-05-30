using System.Text.Json;
using System.Text.Json.Serialization;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Notifications;

/// <summary>Datos listos para publicar en ntfy: a qué URL y con qué cuerpo JSON.</summary>
public sealed record NtfyPublication
{
    /// <summary>Endpoint de publicación (la RAÍZ del servidor; el topic va en el JSON).</summary>
    public required string Url { get; init; }
    /// <summary>Cuerpo JSON (UTF-8) con topic/title/message/priority/tags.</summary>
    public required string JsonBody { get; init; }
}

/// <summary>
/// Construye —de forma PURA y testable— la publicación a ntfy a partir de un
/// <see cref="NotificationMessage"/>. Usa el modo JSON de ntfy (POST a la raíz del
/// servidor con <c>{ "topic", "title", "message", "priority", "tags" }</c>) en vez
/// de cabeceras, para que los títulos con acentos/emoji viajen en UTF-8 sin romper
/// las cabeceras HTTP. El host (Ritmo.App) hace el POST real con HttpClient; aquí
/// solo se decide el QUÉ (sin red → 100% testeable). #122
/// </summary>
public static class NtfyPublish
{
    /// <summary>Servidor por defecto (servicio público de ntfy).</summary>
    public const string DefaultServer = "https://ntfy.sh";

    /// <summary>Normaliza el servidor: vacío → ntfy.sh; recorta y quita la barra final.</summary>
    public static string NormalizeServer(string? server)
    {
        var s = string.IsNullOrWhiteSpace(server) ? DefaultServer : server.Trim();
        return s.TrimEnd('/');
    }

    private sealed class Payload
    {
        [JsonPropertyName("topic")] public string Topic { get; set; } = "";
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("message")] public string Message { get; set; } = "";
        [JsonPropertyName("priority")] public int Priority { get; set; }
        [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
    }

    // UnsafeRelaxedJsonEscaping: deja acentos/emoji literales (UTF-8), no \uXXXX.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Publicación para un aviso. El inicio de sesión va con prioridad alta (4) y
    /// tag de alarma; los avisos previos con prioridad normal (3).
    /// </summary>
    public static NtfyPublication For(string? server, string topic, NotificationMessage msg, PlannedEventType type)
    {
        var payload = new Payload
        {
            Topic = topic.Trim(),
            Title = msg.Title,
            Message = msg.Body,
            Priority = type == PlannedEventType.SessionStart ? 4 : 3,
            Tags = [type == PlannedEventType.SessionStart ? "alarm_clock" : "hourglass_flowing_sand"]
        };
        return new NtfyPublication
        {
            Url = NormalizeServer(server),
            JsonBody = JsonSerializer.Serialize(payload, JsonOpts)
        };
    }

    /// <summary>Publicación de prueba (botón "Probar" en Ajustes / verificación).</summary>
    public static NtfyPublication ForTest(string? server, string topic) => For(
        server, topic,
        new NotificationMessage
        {
            Title = "Ritmo conectado",
            Body = "Si ves esto en el móvil, las notificaciones push funcionan.",
            Tag = "ritmo-test"
        },
        PlannedEventType.SessionStart);
}
