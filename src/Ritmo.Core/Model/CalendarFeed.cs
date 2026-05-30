namespace Ritmo.Core.Model;

/// <summary>
/// Una suscripción a un calendario externo por su enlace ICS (Google "dirección
/// secreta iCal", Outlook "publicar calendario", iCloud "calendario público").
/// Solo lectura: Ritmo descarga el .ics y muestra los eventos. #112
/// </summary>
public sealed record CalendarFeed
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
}
