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
    decimal ProjectedMonthEarnings,
    bool HasProjection = false);

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

    /// <summary>Días transcurridos del mes mínimos antes de mostrar una proyección (evita
    /// extrapolaciones absurdas, p. ej. 20 h el día 1 → 600 h/mes).</summary>
    public const int MinDaysForProjection = 5;

    /// <summary>
    /// Resumen del proyecto para el mes de <paramref name="today"/>. La proyección extrapola
    /// linealmente el ritmo del mes (horas POR DÍA TRABAJADO, no por día de calendario) a los días
    /// LABORABLES restantes; y solo si ya han pasado unos días (<see cref="MinDaysForProjection"/>),
    /// para no dar cifras irreales al principio del mes. <see cref="WorkSummary.HasProjection"/>
    /// indica si la proyección es significativa.
    /// </summary>
    public static WorkSummary Summarize(IEnumerable<WorkLogEntry> log, string projectId, decimal rate, System.DateOnly today)
    {
        var list = log.Where(e => e.ProjectId == projectId).ToList();
        var month = list.Where(e => e.Date.Year == today.Year && e.Date.Month == today.Month).ToList();
        double monthHours = month.Sum(e => e.Hours);
        double totalHours = list.Sum(e => e.Hours);

        int daysInMonth = System.DateTime.DaysInMonth(today.Year, today.Month);
        // Ritmo = horas por DÍA EFECTIVAMENTE TRABAJADO este mes (no por día de calendario), así
        // un único día de 20 h no se extrapola a "600 h/mes". Proyección = ritmo × (días del mes
        // en los que sueles trabajar). Aproximamos esos días como la media observada.
        int daysWorked = month.Select(e => e.Date.Day).Distinct().Count();
        bool hasProjection = today.Day >= MinDaysForProjection && daysWorked > 0;

        double projHours;
        if (hasProjection)
        {
            double hoursPerWorkedDay = monthHours / daysWorked;
            // Fracción de días trabajados sobre los transcurridos, extrapolada a todo el mes.
            double workedDayRate = (double)daysWorked / today.Day;
            double expectedWorkedDays = workedDayRate * daysInMonth;
            projHours = hoursPerWorkedDay * expectedWorkedDays;
        }
        else
        {
            projHours = monthHours;   // aún no proyectamos: mostramos lo real
        }

        return new WorkSummary(
            HoursThisMonth: monthHours,
            EarningsThisMonth: (decimal)monthHours * rate,
            HoursTotal: totalHours,
            EarningsTotal: (decimal)totalHours * rate,
            ProjectedMonthHours: projHours,
            ProjectedMonthEarnings: (decimal)projHours * rate,
            HasProjection: hasProjection);
    }
}
