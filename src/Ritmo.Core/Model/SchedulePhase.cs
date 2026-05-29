namespace Ritmo.Core.Model;

/// <summary>
/// Una "fase" del plan de estudio: un horario semanal con vigencia temporal.
/// Permite que de una fecha a otra rija un horario y, a partir de otra fecha,
/// otro distinto (p. ej. Fase 1 jun–oct, Fase 2 nov–feb...).
///
/// Vigencia: [ValidFrom, ValidTo]. ValidTo nulo = sin fecha de fin (indefinida).
/// Inmutable.
/// </summary>
public sealed record SchedulePhase
{
    public required string Name { get; init; }
    public required DateOnly ValidFrom { get; init; }
    /// <summary>Última fecha en la que la fase sigue vigente (inclusive). Null = indefinida.</summary>
    public DateOnly? ValidTo { get; init; }
    public WeeklySchedule Schedule { get; init; } = new();

    /// <summary>¿Está vigente esta fase en la fecha dada?</summary>
    public bool IsActiveOn(DateOnly date)
    {
        if (date < ValidFrom) return false;
        if (ValidTo is { } end && date > end) return false;
        return true;
    }
}
