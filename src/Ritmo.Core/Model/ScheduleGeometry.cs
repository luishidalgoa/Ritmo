namespace Ritmo.Core.Model;

/// <summary>
/// Geometría pura de la rejilla del horario (#61). Separa dos cosas que antes iban
/// pegadas:
///
///  1. Las **líneas-guía de fondo**, que dependen de la <b>granularidad</b> elegida
///     (60/30/15 min). Es solo una cuadrícula visual uniforme para todos los días.
///  2. La **posición real de cada bloque**, que es proporcional a sus MINUTOS desde
///     el inicio de la rejilla y NO depende de la granularidad. Así un bloque a las
///     16:40 se pinta exactamente donde toca, aunque caiga "entre líneas", sin
///     descuadrar el resto de columnas.
///
/// Todo en píxeles a partir de un alto de hora (<paramref name="hourHeightPx"/>),
/// sin tocar Windows ni la UI → 100% testeable. La UI solo multiplica.
/// </summary>
public static class ScheduleGeometry
{
    /// <summary>Granularidades admitidas para la rejilla de fondo (minutos por línea).</summary>
    public static IReadOnlyList<int> AllowedGranularities { get; } = [60, 30, 15];

    /// <summary>Normaliza a una granularidad válida; cualquier valor raro cae a 60.</summary>
    public static int NormalizeGranularity(int minutes) =>
        minutes is 60 or 30 or 15 ? minutes : 60;

    /// <summary>Cuántas líneas/slots por hora dibuja la granularidad (60→1, 30→2, 15→4).</summary>
    public static int SlotsPerHour(int granularityMinutes) =>
        60 / NormalizeGranularity(granularityMinutes);

    /// <summary>Alto en píxeles de un slot de la rejilla de fondo, manteniendo el alto de hora constante.</summary>
    public static double SlotHeight(double hourHeightPx, int granularityMinutes) =>
        hourHeightPx / SlotsPerHour(granularityMinutes);

    /// <summary>Minutos desde el inicio de la rejilla (la hora <paramref name="startHour"/>) hasta <paramref name="time"/>.</summary>
    public static double MinutesFromStart(TimeOnly time, int startHour) =>
        (time.Hour - startHour) * 60 + time.Minute;

    /// <summary>
    /// Desplazamiento vertical en píxeles del inicio de un bloque, proporcional a su
    /// minuto real (independiente de la granularidad). Relativo al primer borde de la
    /// zona de contenido (debajo de la cabecera).
    /// </summary>
    public static double TopPixels(TimeOnly start, int startHour, double hourHeightPx) =>
        MinutesFromStart(start, startHour) / 60.0 * hourHeightPx;

    /// <summary>Alto en píxeles de un bloque, proporcional a su duración real.</summary>
    public static double HeightPixels(TimeSpan duration, double hourHeightPx) =>
        duration.TotalMinutes / 60.0 * hourHeightPx;

    /// <summary>Número de slots (filas de fondo) para cubrir de <paramref name="startHour"/> a <paramref name="endHour"/>.</summary>
    public static int SlotRows(int startHour, int endHour, int granularityMinutes) =>
        Math.Max(0, endHour - startHour) * SlotsPerHour(granularityMinutes);

    /// <summary>
    /// Convierte un desplazamiento vertical en píxeles a un nº de slots (para el
    /// "ajuste a la rejilla" del arrastre: el preview se mueve por slots de la
    /// granularidad activa). Redondea al slot más cercano.
    /// </summary>
    public static int PixelsToSlots(double deltaPx, double hourHeightPx, int granularityMinutes)
    {
        var slotPx = SlotHeight(hourHeightPx, granularityMinutes);
        if (slotPx <= 0) return 0;
        return (int)Math.Round(deltaPx / slotPx);
    }

    /// <summary>Minutos que dura un slot de la granularidad dada (60/30/15).</summary>
    public static int SlotMinutes(int granularityMinutes) => NormalizeGranularity(granularityMinutes);
}
