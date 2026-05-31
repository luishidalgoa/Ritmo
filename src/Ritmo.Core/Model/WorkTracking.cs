using System.Collections.Generic;
using System.Linq;

namespace Ritmo.Core.Model;

/// <summary>Resumen de seguimiento laboral de un entorno para un mes (#84).</summary>
public sealed record WorkSummary(
    double HoursThisMonth,
    decimal EarningsThisMonth,
    double HoursTotal,
    decimal EarningsTotal,
    double ProjectedMonthHours,
    decimal ProjectedMonthEarnings);

/// <summary>
/// Agrega el registro manual de horas (#84): horas y ganado del mes, total, y una proyección
/// lineal a fin de mes según el ritmo llevado. PURO y testable (recibe el «hoy»).
/// </summary>
public static class WorkTracking
{
    public static double HoursInMonth(IEnumerable<WorkLogEntry> log, string envId, int year, int month)
        => log.Where(e => e.EnvironmentId == envId && e.Date.Year == year && e.Date.Month == month).Sum(e => e.Hours);

    public static double HoursTotal(IEnumerable<WorkLogEntry> log, string envId)
        => log.Where(e => e.EnvironmentId == envId).Sum(e => e.Hours);

    /// <summary>
    /// Resumen del entorno para el mes de <paramref name="today"/>. La proyección extrapola
    /// linealmente el ritmo del mes (horas/día transcurrido) a los días totales del mes.
    /// </summary>
    public static WorkSummary Summarize(IEnumerable<WorkLogEntry> log, string envId, decimal rate, System.DateOnly today)
    {
        var list = log.Where(e => e.EnvironmentId == envId).ToList();
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
