namespace Ritmo.Core.Model;

/// <summary>
/// Reglas puras de habilitación de slots de hora/minuto para un selector de hora con
/// un mínimo EXCLUSIVO (#150): la hora elegida debe ser estrictamente posterior a
/// <c>minExclusive</c> (p. ej. la hora de fin debe superar a la de inicio). Sin UI:
/// el control de WinUI pinta en gris y bloquea los slots que estas reglas marcan inválidos.
/// </summary>
public static class TimeSlots
{
    /// <summary>
    /// ¿La columna de la hora <paramref name="hour"/> tiene ALGÚN minuto válido (del paso dado)?
    /// Si no, la hora entera se deshabilita. Sin mínimo, siempre válida.
    /// </summary>
    public static bool HourEnabled(int hour, int minuteStep, System.TimeOnly? minExclusive)
    {
        if (minExclusive is not { } min) return true;
        int step = minuteStep < 1 ? 1 : minuteStep;
        int lastMinute = 60 - step;                      // último slot de la hora (p. ej. 55)
        return new System.TimeOnly(hour, lastMinute) > min;
    }

    /// <summary>
    /// ¿El minuto <paramref name="minute"/> de la hora <paramref name="hour"/> es válido?
    /// (estrictamente posterior al mínimo). Sin mínimo, siempre válido.
    /// </summary>
    public static bool MinuteEnabled(int hour, int minute, System.TimeOnly? minExclusive)
        => minExclusive is not { } min || new System.TimeOnly(hour, minute) > min;

    /// <summary>Primer minuto válido (del paso) para una hora, o 0 si ninguno aplica.</summary>
    public static int FirstValidMinute(int hour, int minuteStep, System.TimeOnly? minExclusive)
    {
        int step = minuteStep < 1 ? 1 : minuteStep;
        for (int m = 0; m < 60; m += step)
            if (MinuteEnabled(hour, m, minExclusive)) return m;
        return 0;
    }
}
