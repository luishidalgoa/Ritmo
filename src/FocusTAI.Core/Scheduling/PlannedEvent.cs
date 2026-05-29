using FocusTAI.Core.Model;

namespace FocusTAI.Core.Scheduling;

/// <summary>Qué representa un evento planificado en el tiempo.</summary>
public enum PlannedEventType
{
    /// <summary>Aviso previo: "tu sesión empieza en X minutos".</summary>
    PreAlert,
    /// <summary>Momento de iniciar la sesión de concentración.</summary>
    SessionStart
}

/// <summary>
/// Un evento concreto situado en una fecha/hora absoluta, derivado de una sesión.
/// Lo produce el planificador para que el host (UI/SO) sepa qué hacer y cuándo.
/// </summary>
public sealed record PlannedEvent
{
    public required DateTime At { get; init; }
    public required PlannedEventType Type { get; init; }
    public required StudySession Session { get; init; }
    /// <summary>Solo para PreAlert: minutos antes del inicio.</summary>
    public int? MinutesBefore { get; init; }

    /// <summary>Fecha/hora absoluta del inicio de la sesión asociada.</summary>
    public DateTime SessionStartAt { get; init; }
}
