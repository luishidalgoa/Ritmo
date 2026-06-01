namespace Ritmo.Core.Model;

/// <summary>
/// Una anotación de horas trabajadas en un PROYECTO un día concreto (#84). El seguimiento laboral
/// es MANUAL y acumulativo (perfiles sin horario fijo): el usuario va sumando horas día a día.
/// Inmutable y puro.
/// </summary>
public sealed record WorkLogEntry
{
    /// <summary>Identificador estable (para editarla/borrarla).</summary>
    public required string Id { get; init; }
    /// <summary>Proyecto al que pertenecen las horas (ver <see cref="WorkProject"/>). #84 V3</summary>
    public required string ProjectId { get; init; }
    /// <summary>Día trabajado.</summary>
    public required System.DateOnly Date { get; init; }
    /// <summary>Horas trabajadas ese día (puede acumular varias anotaciones por día).</summary>
    public required double Hours { get; init; }
    /// <summary>Nota opcional de la anotación (p. ej. qué se hizo). #84 V3</summary>
    public string Note { get; init; } = "";
}
