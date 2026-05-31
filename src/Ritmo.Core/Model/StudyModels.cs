namespace Ritmo.Core.Model;

/// <summary>
/// Un aviso previo al inicio de una sesión, expresado en minutos antes.
/// Ej.: 60 = "una hora antes", 5 = "cinco minutos antes".
/// </summary>
public readonly record struct PreAlert(int MinutesBefore)
{
    public static PreAlert OneHour => new(60);
    public static PreAlert TenMinutes => new(10);
    public static PreAlert FiveMinutes => new(5);

    public override string ToString() =>
        MinutesBefore >= 60 && MinutesBefore % 60 == 0
            ? $"{MinutesBefore / 60} h antes"
            : $"{MinutesBefore} min antes";
}

/// <summary>
/// Una sesión de concentración programada dentro de la semana:
/// día de la semana + hora de inicio + duración + tipo + avisos previos.
/// Es inmutable y puro: no conoce el SO ni la UI.
/// </summary>
public sealed record StudySession
{
    public required string Title { get; init; }
    public required DayOfWeek Day { get; init; }
    /// <summary>Hora de inicio (local) dentro del día.</summary>
    public required TimeOnly Start { get; init; }
    /// <summary>Duración del bloque.</summary>
    public required TimeSpan Duration { get; init; }
    /// <summary>Id de la categoría de bloque (ver <see cref="BlockCategory"/>). #83</summary>
    public string CategoryId { get; init; } = CategoryIds.Other;
    /// <summary>Avisos previos configurables (puede haber 0, 1 o varios).</summary>
    public IReadOnlyList<PreAlert> PreAlerts { get; init; } = [];

    /// <summary>
    /// Bloque provisional: el usuario aún no sabe qué pasará en este hueco.
    /// Se muestra atenuado y NO dispara el modo concentración automáticamente
    /// (sus avisos previos sí pueden sonar, como recordatorio suave).
    /// </summary>
    public bool IsTentative { get; init; }

    public TimeOnly End => Start.Add(Duration);
}

/// <summary>
/// Horario semanal completo: la colección de sesiones de la semana.
/// </summary>
public sealed record WeeklySchedule
{
    public IReadOnlyList<StudySession> Sessions { get; init; } = [];
}
