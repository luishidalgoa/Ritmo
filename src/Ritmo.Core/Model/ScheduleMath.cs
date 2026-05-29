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

    /// <summary>
    /// Desplaza una hora de inicio <paramref name="slotDelta"/> "slots" (de
    /// <paramref name="slotMinutes"/> minutos cada uno). El resultado se acota al
    /// día [00:00 .. último slot que empieza dentro del día], para mover sesiones
    /// arrastrándolas verticalmente sin que la hora se salga del día. #82
    /// </summary>
    public static System.TimeOnly ShiftStart(System.TimeOnly start, int slotDelta, int slotMinutes = 30)
    {
        int min = start.Hour * 60 + start.Minute + slotDelta * slotMinutes;
        min = System.Math.Clamp(min, 0, 1440 - slotMinutes);
        return new System.TimeOnly(min / 60, min % 60);
    }
}
