namespace Ritmo.Core.Model;

/// <summary>
/// Una anotación de horas trabajadas en un entorno/proyecto un día concreto (#84). El seguimiento
/// laboral es MANUAL y acumulativo (perfiles sin horario fijo): el usuario va sumando horas día a
/// día. Inmutable y puro.
/// </summary>
public sealed record WorkLogEntry
{
    /// <summary>Identificador estable (para editarla/borrarla).</summary>
    public required string Id { get; init; }
    /// <summary>Entorno/proyecto al que pertenecen las horas.</summary>
    public required string EnvironmentId { get; init; }
    /// <summary>Día trabajado.</summary>
    public required System.DateOnly Date { get; init; }
    /// <summary>Horas trabajadas ese día (puede acumular varias anotaciones por día).</summary>
    public required double Hours { get; init; }
}
