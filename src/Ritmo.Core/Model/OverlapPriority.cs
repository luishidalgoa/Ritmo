namespace Ritmo.Core.Model;

/// <summary>
/// Decisión del usuario ante un solapamiento horario↔calendario (#114): para un
/// evento concreto, qué lado prioriza. Se recuerda por la clave estable del evento
/// (ver <see cref="Scheduling.OverlapResolver.EventKey"/>) y solo afecta a la
/// presentación: no borra ni mueve nada.
/// </summary>
public sealed record OverlapPriority
{
    public required string EventKey { get; init; }

    /// <summary>true = prioriza el evento del calendario; false = prioriza la sesión del horario.</summary>
    public required bool PreferCalendar { get; init; }
}
