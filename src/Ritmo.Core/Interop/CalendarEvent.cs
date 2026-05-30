namespace Ritmo.Core.Interop;

/// <summary>
/// Un evento concreto (con fecha) leído de un calendario externo vía ICS. A
/// diferencia del horario semanal recurrente, esto es un compromiso puntual
/// (reunión, cita…) que se muestra junto al plan. #112
/// </summary>
public sealed record CalendarEvent(
    string Title,
    System.DateTime Start,
    System.DateTime End,
    bool AllDay,
    string? Calendar = null);
