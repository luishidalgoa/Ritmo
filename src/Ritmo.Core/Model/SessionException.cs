namespace Ritmo.Core.Model;

/// <summary>
/// Excepción de una sesión recurrente (#137): en un rango de fechas (un día suelto = From == To), la
/// sesión NO se realiza, o se realiza solo PARCIALMENTE. Sirve para «hoy no fui a trabajar», «hoy
/// salí antes» o ausencias largas. Afecta al cómputo de horas del seguimiento laboral y a cómo se
/// pinta la sesión (atenuada/tachada si no se hizo; con las horas reales si fue parcial).
///
/// La sesión se identifica por una CLAVE estable (título+categoría+inicio+duración), no por índice,
/// para sobrevivir a reordenamientos. Inmutable.
/// </summary>
public sealed record SessionException
{
    /// <summary>Identificador estable (para quitar la excepción).</summary>
    public required string Id { get; init; }
    /// <summary>Clave de la sesión afectada (ver <see cref="SessionKey.For"/>).</summary>
    public required string SessionKey { get; init; }
    /// <summary>Primer día afectado (inclusive).</summary>
    public required System.DateOnly From { get; init; }
    /// <summary>Último día afectado (inclusive).</summary>
    public required System.DateOnly To { get; init; }
    /// <summary>Motivo opcional (p. ej. «festivo», «baja»).</summary>
    public string Reason { get; init; } = "";

    /// <summary>
    /// Horas REALES computadas esos días (#137b): null = NO realizada (0 h y se tacha); un valor =
    /// realizada PARCIALMENTE (computa esas horas en vez de la duración completa de la sesión).
    /// </summary>
    public double? ActualHours { get; init; }

    /// <summary>¿No se realizó en absoluto? (frente a parcial).</summary>
    public bool IsNotDone => ActualHours is null;

    /// <summary>¿La fecha cae dentro de la excepción?</summary>
    public bool Covers(System.DateOnly d) => d >= From && d <= To;
}

/// <summary>Clave estable de una sesión del horario, para asociarle excepciones (#137).</summary>
public static class SessionKey
{
    public static string For(StudySession s)
        => $"{s.Title.Trim()}|{s.CategoryId}|{s.Start}|{s.Duration}";
}
