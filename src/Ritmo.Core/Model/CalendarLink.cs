namespace Ritmo.Core.Model;

/// <summary>
/// Vínculo entre una sesión publicada y su evento en el calendario externo (#112 Fase 2): mapea la
/// clave estable del spec (<see cref="Sync.CalendarEventSpec.Key"/>) con el id del evento creado, para
/// ACTUALIZAR en vez de duplicar al volver a publicar, y para borrar los que ya no existan.
/// </summary>
public sealed record CalendarLink
{
    public required string Key { get; init; }
    public required string EventId { get; init; }
}
