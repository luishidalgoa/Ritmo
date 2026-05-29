namespace Ritmo.Core.Model;

/// <summary>
/// Tipo de bloque de estudio. Coincide con los carriles del horario TAI.
/// </summary>
public enum StudyKind
{
    Tecnico,
    Legislacion,
    Ingles,
    Tests,
    Simulacro,
    Descanso,
    /// <summary>Hueco reservado para estudiar, pero sin materia decidida todavía.</summary>
    PorDefinir,
    /// <summary>Evento personal del usuario (comida, deporte, ocio…). No es de estudio.</summary>
    Personal,
    Otro
}

/// <summary>Utilidades sobre el tipo de bloque.</summary>
public static class StudyKindExtensions
{
    /// <summary>
    /// ¿Es un tipo "de concentración"? Solo estos disparan el modo focus.
    /// Los demás (Descanso, Personal, PorDefinir) se ven en el horario pero NO
    /// arrancan concentración: sirven para reflejar la semana completa.
    /// </summary>
    public static bool IsFocusKind(this StudyKind kind) => kind switch
    {
        StudyKind.Tecnico => true,
        StudyKind.Legislacion => true,
        StudyKind.Ingles => true,
        StudyKind.Tests => true,
        StudyKind.Simulacro => true,
        _ => false   // Descanso, PorDefinir, Personal, Otro -> no disparan concentración
    };

    /// <summary>Etiqueta legible en español para mostrar en UI/avisos.</summary>
    public static string Label(this StudyKind kind) => kind switch
    {
        StudyKind.Tecnico => "Técnico",
        StudyKind.Legislacion => "Legislación",
        StudyKind.Ingles => "Inglés",
        StudyKind.Tests => "Tests",
        StudyKind.Simulacro => "Simulacro",
        StudyKind.Descanso => "Descanso",
        StudyKind.PorDefinir => "Por definir",
        StudyKind.Personal => "Personal",
        _ => "Otro"
    };
}

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
    public StudyKind Kind { get; init; } = StudyKind.Otro;
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
