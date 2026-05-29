using Ritmo.Core.Model;
using Ritmo.Core.Pomodoro;

namespace Ritmo.Core.Persistence;

/// <summary>
/// Estado persistente de la app: el horario semanal y la configuración Pomodoro.
/// Es lo que se guarda/carga en disco. Inmutable.
/// </summary>
public sealed record AppSettings
{
    public WeeklySchedule Schedule { get; init; } = new();
    public PomodoroConfig Pomodoro { get; init; } = PomodoroConfig.DeepWork;

    public static AppSettings Default => new();
}
