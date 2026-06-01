using System;
using System.Collections.Generic;
using System.Linq;

namespace Ritmo.Core.Model;

/// <summary>
/// Cómputo AUTOMÁTICO de horas a partir de las sesiones del horario vinculadas a un proyecto (#137).
/// PURO y testable. Para un proyecto y un rango de fechas, recorre los días, mira qué sesiones
/// vinculadas a ese proyecto «tocan» ese día (por su día de la semana) y NO están canceladas por una
/// excepción, y suma su duración. Resultado: horas por día (índice 0 = día 1 del mes).
/// </summary>
public static class WorkAutoCompute
{
    /// <summary>
    /// Horas automáticas por DÍA del mes para un proyecto, a partir de las sesiones del
    /// <paramref name="schedule"/> vinculadas a él. <paramref name="exceptions"/> cancela días.
    /// </summary>
    public static double[] DailyAutoHours(
        IReadOnlyList<StudySession> schedule,
        IReadOnlyList<SessionException> exceptions,
        string projectId, int year, int month)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        var result = new double[daysInMonth];

        var linked = schedule.Where(s => s.ProjectId == projectId).ToList();
        if (linked.Count == 0) return result;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            foreach (var s in linked.Where(s => s.Day == date.DayOfWeek))
            {
                var key = SessionKey.For(s);
                var exc = exceptions.FirstOrDefault(x => x.SessionKey == key && x.Covers(date));
                // Sin excepción: duración completa. No realizada (ActualHours null): 0.
                // Parcial: las horas reales indicadas. #137 / #137b
                if (exc is null) result[day - 1] += s.Duration.TotalHours;
                else if (exc.ActualHours is { } h) result[day - 1] += h;
            }
        }
        return result;
    }

    /// <summary>Total de horas automáticas del mes para un proyecto (suma de <see cref="DailyAutoHours"/>).</summary>
    public static double MonthAutoHours(
        IReadOnlyList<StudySession> schedule,
        IReadOnlyList<SessionException> exceptions,
        string projectId, int year, int month)
        => DailyAutoHours(schedule, exceptions, projectId, year, month).Sum();

    /// <summary>
    /// ¿La sesión <paramref name="s"/> está cancelada el día <paramref name="date"/>? (para pintarla
    /// atenuada/tachada en el horario). #137
    /// </summary>
    public static bool IsCancelled(StudySession s, DateOnly date, IReadOnlyList<SessionException> exceptions)
    {
        var key = SessionKey.For(s);
        return exceptions.Any(x => x.SessionKey == key && x.Covers(date));
    }

    /// <summary>Excepción aplicable a una sesión en una fecha (o null si no hay). #137b</summary>
    public static SessionException? ExceptionFor(StudySession s, DateOnly date, IReadOnlyList<SessionException> exceptions)
    {
        var key = SessionKey.For(s);
        return exceptions.FirstOrDefault(x => x.SessionKey == key && x.Covers(date));
    }

    /// <summary>
    /// Entradas de log VIRTUALES (no persistidas) con las horas automáticas de las sesiones
    /// vinculadas a un proyecto en un mes (#137). Una por día con horas. Permiten reutilizar todo el
    /// agregador <c>WorkTracking</c> combinándolas con el log manual.
    /// </summary>
    public static IReadOnlyList<WorkLogEntry> VirtualEntriesForMonth(
        IReadOnlyList<StudySession> schedule,
        IReadOnlyList<SessionException> exceptions,
        string projectId, int year, int month)
    {
        var daily = DailyAutoHours(schedule, exceptions, projectId, year, month);
        var list = new List<WorkLogEntry>();
        for (int d = 0; d < daily.Length; d++)
            if (daily[d] > 0)
                list.Add(new WorkLogEntry
                {
                    Id = $"auto-{projectId}-{year}{month:00}{d + 1:00}",
                    ProjectId = projectId, Date = new DateOnly(year, month, d + 1), Hours = daily[d]
                });
        return list;
    }
}
