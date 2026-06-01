namespace Ritmo.Core.Model;

/// <summary>
/// Sesión "provisional" / extraordinaria (#103): NO es recurrente, ocurre en una
/// FECHA concreta (p. ej. "este jueves tengo una clase extra"). Se superpone a la
/// rejilla del horario solo en la semana que contiene su fecha. Inmutable y pura.
/// </summary>
public sealed record OneOffSession
{
    /// <summary>Identificador estable (para borrarla).</summary>
    public required string Id { get; init; }
    /// <summary>Fecha concreta en la que ocurre.</summary>
    public required System.DateOnly Date { get; init; }
    public required string Title { get; init; }
    public required System.TimeOnly Start { get; init; }
    public required System.TimeSpan Duration { get; init; }
    /// <summary>Id de la categoría de bloque (ver <see cref="BlockCategory"/>). #83</summary>
    public string CategoryId { get; init; } = CategoryIds.Other;
    public IReadOnlyList<PreAlert> PreAlerts { get; init; } = [];
    public bool IsTentative { get; init; }
    /// <summary>Proyecto de seguimiento laboral vinculado (#137), o null. Sus horas computan ese día.</summary>
    public string? ProjectId { get; init; }

    /// <summary>Vista como <see cref="StudySession"/> (Day derivado de la fecha) para reutilizar el render.</summary>
    public StudySession AsSession() => new()
    {
        Title = Title,
        Day = Date.DayOfWeek,
        Start = Start,
        Duration = Duration,
        CategoryId = CategoryId,
        PreAlerts = PreAlerts,
        IsTentative = IsTentative,
        ProjectId = ProjectId
    };
}
