namespace Ritmo.Core.Model;

/// <summary>Un enlace-atajo: un acceso rápido a un recurso (documentación, web…).</summary>
public sealed record ShortcutLink
{
    public required string Title { get; init; }
    public required string Url { get; init; }
}

/// <summary>
/// Cómo se ve la rejilla del horario (estilo Excel) y atajos asociados.
/// No afecta a la lógica de planificación; es pura presentación + preferencias.
/// </summary>
public sealed record ScheduleViewConfig
{
    /// <summary>Primera hora mostrada en las filas (p. ej. 08:00).</summary>
    public TimeOnly DayStart { get; init; } = new(8, 0);
    /// <summary>Última hora mostrada en las filas (p. ej. 20:00).</summary>
    public TimeOnly DayEnd { get; init; } = new(20, 0);

    /// <summary>
    /// Granularidad de la rejilla de fondo en minutos: 60 (por defecto), 30 o 15.
    /// SOLO afecta a las líneas-guía uniformes; los bloques se posicionan por su
    /// minuto real (independiente de este valor), así una sesión a las 16:40 se ve
    /// donde toca sin desalinear el resto de días. #61
    /// </summary>
    public int GranularityMinutes { get; init; } = 60;

    /// <summary>Enlaces-atajo a recursos (clicables desde el horario).</summary>
    public IReadOnlyList<ShortcutLink> Shortcuts { get; init; } = [];

    /// <summary>Si al iniciar concentración se muestra la vista previa del día.</summary>
    public bool ShowDayPreviewOnFocusStart { get; init; } = true;

    /// <summary>Número de filas (horas) que ocupa la rejilla. 0 si el rango es inválido.</summary>
    public int RowCount
    {
        get
        {
            var minutes = (DayEnd.ToTimeSpan() - DayStart.ToTimeSpan()).TotalMinutes;
            return minutes <= 0 ? 0 : (int)Math.Ceiling(minutes / 60.0);
        }
    }
}
