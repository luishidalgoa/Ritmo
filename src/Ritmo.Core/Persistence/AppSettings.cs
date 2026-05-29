using Ritmo.Core.Model;
using Ritmo.Core.Pomodoro;

namespace Ritmo.Core.Persistence;

/// <summary>
/// Estado persistente completo de la app. Inmutable.
///
/// Compatibilidad: <see cref="Schedule"/> es el horario "suelto" del formato
/// original (sin fases). <see cref="Plan"/> es el horario por fases temporales
/// (lo nuevo y principal). Un JSON antiguo (solo sesiones) sigue cargándose en
/// Schedule; uno nuevo usa Plan.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Horario semanal sin fases (formato original / compatibilidad).</summary>
    public WeeklySchedule Schedule { get; init; } = new();

    /// <summary>Horario por fases temporales (de X a Y fecha). Fuente principal.</summary>
    public SchedulePlan Plan { get; init; } = new();

    public PomodoroConfig Pomodoro { get; init; } = PomodoroConfig.DeepWork;

    /// <summary>Notas fijadas por el usuario.</summary>
    public IReadOnlyList<StudyNote> Notes { get; init; } = [];

    /// <summary>Preferencias de visualización del horario.</summary>
    public ScheduleViewConfig ViewConfig { get; init; } = new();

    public static AppSettings Default => new();
}
