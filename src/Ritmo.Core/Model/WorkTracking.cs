using System.Collections.Generic;
using System.Linq;

namespace Ritmo.Core.Model;

/// <summary>Resumen de seguimiento laboral de un proyecto para un mes (#84).</summary>
public sealed record WorkSummary(
    double HoursThisMonth,
    decimal EarningsThisMonth,
    double HoursTotal,
    decimal EarningsTotal,
    double ProjectedMonthHours,
    decimal ProjectedMonthEarnings);

/// <summary>
/// Agrega el registro manual de horas por PROYECTO (#84 V3): horas y ganado del mes, total, y una
/// proyección lineal a fin de mes según el ritmo llevado. PURO y testable (recibe el «hoy»).
/// </summary>
public static class WorkTracking
{
    public static double HoursInMonth(IEnumerable<WorkLogEntry> log, string projectId, int year, int month)
        => log.Where(e => e.ProjectId == projectId && e.Date.Year == year && e.Date.Month == month).Sum(e => e.Hours);

    public static double HoursTotal(IEnumerable<WorkLogEntry> log, string projectId)
        => log.Where(e => e.ProjectId == projectId).Sum(e => e.Hours);

    /// <summary>
    /// Horas trabajadas por DÍA del mes (índice 0 = día 1). Para el gráfico de barras (#84).
    /// </summary>
    public static double[] DailyHours(IEnumerable<WorkLogEntry> log, string projectId, int year, int month)
    {
        var days = new double[System.DateTime.DaysInMonth(year, month)];
        foreach (var e in log.Where(e => e.ProjectId == projectId && e.Date.Year == year && e.Date.Month == month))
            days[e.Date.Day - 1] += e.Hours;
        return days;
    }

    /// <summary>
    /// Horas ACUMULADAS por día del mes (índice 0 = día 1): cada posición es la suma hasta ese día.
    /// Para la línea «acumulado vs objetivo» (#84 V3).
    /// </summary>
    public static double[] CumulativeHours(IEnumerable<WorkLogEntry> log, string projectId, int year, int month)
    {
        var daily = DailyHours(log, projectId, year, month);
        var cum = new double[daily.Length];
        double acc = 0;
        for (int i = 0; i < daily.Length; i++) { acc += daily[i]; cum[i] = acc; }
        return cum;
    }

    /// <summary>Progreso 0..1 del mes frente al objetivo (0 si no hay objetivo). #84 V2</summary>
    public static double GoalProgress(double hoursThisMonth, double monthlyGoalHours)
        => monthlyGoalHours > 0 ? hoursThisMonth / monthlyGoalHours : 0;

    /// <summary>
    /// Resumen del proyecto para el mes de <paramref name="today"/>. La proyección extrapola
    /// linealmente el ritmo del mes (horas/día transcurrido) a los días totales del mes.
    /// </summary>
    public static WorkSummary Summarize(IEnumerable<WorkLogEntry> log, string projectId, decimal rate, System.DateOnly today)
    {
        var list = log.Where(e => e.ProjectId == projectId).ToList();
        double monthHours = list.Where(e => e.Date.Year == today.Year && e.Date.Month == today.Month).Sum(e => e.Hours);
        double totalHours = list.Sum(e => e.Hours);

        int daysInMonth = System.DateTime.DaysInMonth(today.Year, today.Month);
        double projHours = today.Day > 0 ? monthHours / today.Day * daysInMonth : monthHours;

        return new WorkSummary(
            HoursThisMonth: monthHours,
            EarningsThisMonth: (decimal)monthHours * rate,
            HoursTotal: totalHours,
            EarningsTotal: (decimal)totalHours * rate,
            ProjectedMonthHours: projHours,
            ProjectedMonthEarnings: (decimal)projHours * rate);
    }
}
