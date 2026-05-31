using System.Collections.Generic;
using System.Linq;
using Ritmo.Core.Notifications;
using Ritmo.Core.Scheduling;

namespace Ritmo_App.Services;

/// <summary>
/// Un canal por el que se entrega una notificación al usuario (toast del SO, ntfy al móvil…).
/// Cada canal es best-effort: si falla, NO debe tumbar a los demás (lo garantiza el hub).
/// </summary>
public interface INotificationChannel
{
    /// <summary>Identificador estable del canal (evita registrarlo dos veces).</summary>
    string Name { get; }
    /// <summary>Entrega el mensaje por este canal.</summary>
    void Send(NotificationMessage message, PlannedEventType kind);
}

/// <summary>
/// Núcleo CENTRALIZADO de notificaciones (#128): TODA notificación de la app pasa por aquí.
/// El hub no sabe "cómo" se notifica; solo reparte el mensaje a los canales registrados
/// (Stripe-Elements / publish-subscribe). Hoy: toast de Windows + ntfy al móvil; añadir un
/// canal nuevo = registrar otro <see cref="INotificationChannel"/>, sin tocar a los emisores.
/// </summary>
public sealed class NotificationHub
{
    public static NotificationHub Instance { get; } = new();

    private readonly List<INotificationChannel> _channels = [];
    private readonly object _lock = new();

    private NotificationHub() { }

    /// <summary>Registra un canal (idempotente por <see cref="INotificationChannel.Name"/>).</summary>
    public void Register(INotificationChannel channel)
    {
        lock (_lock)
            if (!_channels.Any(c => c.Name == channel.Name)) _channels.Add(channel);
    }

    /// <summary>Nombres de los canales registrados (para diagnóstico/MCP).</summary>
    public IReadOnlyList<string> Channels
    {
        get { lock (_lock) return _channels.Select(c => c.Name).ToList(); }
    }

    /// <summary>
    /// Punto ÚNICO de salida: reparte el mensaje a todos los canales. Cada canal es
    /// best-effort y aislado (una excepción en uno no impide los demás).
    /// </summary>
    public void Notify(NotificationMessage message, PlannedEventType kind)
    {
        INotificationChannel[] channels;
        lock (_lock) channels = _channels.ToArray();
        foreach (var ch in channels)
        {
            try { ch.Send(message, kind); }
            catch { /* un canal caído no debe afectar al resto */ }
        }
    }
}

/// <summary>Canal: toast de Windows (App Notifications). #128</summary>
internal sealed class ToastChannel : INotificationChannel
{
    public string Name => "toast";
    public void Send(NotificationMessage message, PlannedEventType kind) => ToastService.Show(message);
}

/// <summary>Canal: push al móvil vía ntfy (opt-in; lee servidor/topic de los ajustes). #122/#128</summary>
internal sealed class NtfyChannel : INotificationChannel
{
    public string Name => "ntfy";
    public void Send(NotificationMessage message, PlannedEventType kind)
    {
        var s = AppState.Load();
        if (s.NtfyEnabled && !string.IsNullOrWhiteSpace(s.NtfyTopic))
            _ = NtfyPublisher.PublishAsync(NtfyPublish.For(s.NtfyServerUrl, s.NtfyTopic!, message, kind));
    }
}
