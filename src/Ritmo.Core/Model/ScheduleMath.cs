namespace Ritmo.Core.Model;

/// <summary>Cálculos puros sobre horas del horario. Testable sin UI.</summary>
public static class ScheduleMath
{
    /// <summary>
    /// Duración entre dos horas del día. Si <paramref name="end"/> es anterior o
    /// igual a <paramref name="start"/> se asume que cruza la medianoche (p. ej.
    /// 23:00→01:00 = 2h). Si son exactamente iguales, devuelve cero (bloque inválido).
    /// </summary>
    public static System.TimeSpan DurationBetween(System.TimeOnly start, System.TimeOnly end)
    {
        var d = end.ToTimeSpan() - start.ToTimeSpan();
        if (d < System.TimeSpan.Zero) d += System.TimeSpan.FromHours(24);   // cruza medianoche
        return d;   // == 0 si start == end (lo rechaza la validación de sesión)
    }
}
