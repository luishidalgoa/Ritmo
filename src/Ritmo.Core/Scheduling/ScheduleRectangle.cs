using System;

namespace Ritmo.Core.Scheduling;

/// <summary>
/// Selección rectangular tipo hoja de cálculo en el horario semanal (#142): el usuario clica una
/// sesión A y, con Shift, otra B; queda seleccionado todo lo que cae dentro del rectángulo de
/// columnas (días) × franja horaria que abarcan A y B.
///
/// Función PURA: decide si una sesión (en su columna <paramref name="dayIndex"/> y con su franja
/// [start, end)) está dentro del rectángulo [dayMin..dayMax] × [timeMin, timeMax]. Una sesión cuenta
/// si su día está dentro del rango de columnas y su franja SOLAPA el rango horario.
/// </summary>
public static class ScheduleRectangle
{
    public static bool InRectangle(int dayIndex, TimeOnly start, TimeOnly end,
        int dayMin, int dayMax, TimeOnly timeMin, TimeOnly timeMax)
    {
        if (dayIndex < dayMin || dayIndex > dayMax) return false;
        return start < timeMax && end > timeMin;   // solape de la franja horaria
    }
}
