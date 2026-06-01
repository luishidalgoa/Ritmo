namespace Ritmo.Core.Model;

/// <summary>
/// Excepción de una sesión recurrente (#137): marca que una sesión NO se realiza en un rango de
/// fechas (un día suelto = From == To). Sirve para «hoy no fui a trabajar» o ausencias largas. Los
/// días cubiertos no computan horas en el seguimiento laboral y la sesión se ve atenuada/tachada.
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
    /// <summary>Primer día sin realizar (inclusive).</summary>
    public required System.DateOnly From { get; init; }
    /// <summary>Último día sin realizar (inclusive).</summary>
    public required System.DateOnly To { get; init; }
    /// <summary>Motivo opcional (p. ej. «festivo», «baja»).</summary>
    public string Reason { get; init; } = "";

    /// <summary>¿La fecha cae dentro de la excepción?</summary>
    public bool Covers(System.DateOnly d) => d >= From && d <= To;
}

/// <summary>Clave estable de una sesión del horario, para asociarle excepciones (#137).</summary>
public static class SessionKey
{
    public static string For(StudySession s)
        => $"{s.Title.Trim()}|{s.CategoryId}|{s.Start}|{s.Duration}";
}
