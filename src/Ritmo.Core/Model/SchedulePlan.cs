namespace Ritmo.Core.Model;

/// <summary>
/// Plan completo: las fases temporales del horario ordenadas en el tiempo.
/// Sabe qué fase está vigente en una fecha y cuál es la siguiente (para avisar
/// de los cambios de fase y para dejar preparar versiones futuras con antelación).
/// Inmutable y puro.
/// </summary>
public sealed record SchedulePlan
{
    public IReadOnlyList<SchedulePhase> Phases { get; init; } = [];

    /// <summary>Fases ordenadas por fecha de inicio ascendente.</summary>
    public IReadOnlyList<SchedulePhase> OrderedPhases =>
        Phases.OrderBy(p => p.ValidFrom).ToList();

    /// <summary>
    /// Fase vigente en la fecha dada. Si varias solapan, gana la de inicio más
    /// reciente que siga siendo válida (la "más específica" para esa fecha).
    /// Null si ninguna fase cubre esa fecha.
    /// </summary>
    public SchedulePhase? GetActivePhase(DateOnly date) =>
        Phases.Where(p => p.IsActiveOn(date))
              .OrderByDescending(p => p.ValidFrom)
              .FirstOrDefault();

    /// <summary>
    /// La próxima fase que empieza estrictamente DESPUÉS de la fecha dada
    /// (para "tu Fase 2 empieza el 1 de noviembre"). Null si no hay ninguna futura.
    /// </summary>
    public SchedulePhase? GetNextPhase(DateOnly date) =>
        Phases.Where(p => p.ValidFrom > date)
              .OrderBy(p => p.ValidFrom)
              .FirstOrDefault();

    /// <summary>
    /// El horario semanal vigente en la fecha (el de la fase activa), o un
    /// horario vacío si no hay fase activa. Atajo cómodo para el resto del sistema.
    /// </summary>
    public WeeklySchedule GetActiveSchedule(DateOnly date) =>
        GetActivePhase(date)?.Schedule ?? new WeeklySchedule();

    /// <summary>
    /// Detecta solapes entre fases (mismo día cubierto por dos fases). Útil para
    /// avisar en el editor; no es un error fatal porque GetActivePhase desempata.
    /// Devuelve los pares de nombres que se solapan.
    /// </summary>
    public IReadOnlyList<(string A, string B)> FindOverlaps()
    {
        var result = new List<(string, string)>();
        var ordered = OrderedPhases;
        for (int i = 0; i < ordered.Count; i++)
        {
            for (int j = i + 1; j < ordered.Count; j++)
            {
                if (Overlap(ordered[i], ordered[j]))
                    result.Add((ordered[i].Name, ordered[j].Name));
            }
        }
        return result;
    }

    private static bool Overlap(SchedulePhase a, SchedulePhase b)
    {
        var aEnd = a.ValidTo ?? DateOnly.MaxValue;
        var bEnd = b.ValidTo ?? DateOnly.MaxValue;
        // Solapan si cada una empieza antes (o igual) de que la otra termine.
        return a.ValidFrom <= bEnd && b.ValidFrom <= aEnd;
    }
}
