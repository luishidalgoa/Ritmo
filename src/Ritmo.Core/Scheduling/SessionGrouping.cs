using System;
using System.Collections.Generic;
using System.Linq;
using Ritmo.Core.Model;

namespace Ritmo.Core.Scheduling;

/// <summary>
/// Agrupación de sesiones por TÍTULO (#116): el "tipo de sesión" con el que se
/// configura el comportamiento de concentración. Puro y testable.
/// </summary>
public static class SessionGrouping
{
    /// <summary>Títulos distintos (normalizados, sin vacíos) de una colección de sesiones, ordenados.</summary>
    public static IReadOnlyList<string> DistinctTitles(IEnumerable<StudySession> sessions)
        => sessions.Select(s => s.Title.Trim())
                   .Where(t => t.Length > 0)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)
                   .ToList();

    /// <summary>Todos los títulos distintos del plan (todas las fases) + el horario suelto.</summary>
    public static IReadOnlyList<string> AllTitles(SchedulePlan plan, WeeklySchedule? loose = null)
    {
        var all = plan.Phases.SelectMany(p => p.Schedule.Sessions);
        if (loose is not null) all = all.Concat(loose.Sessions);
        return DistinctTitles(all);
    }
}
