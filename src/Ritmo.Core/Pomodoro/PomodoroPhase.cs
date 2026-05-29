namespace Ritmo.Core.Pomodoro;

/// <summary>Fase actual del ciclo Pomodoro.</summary>
public enum PomodoroPhase
{
    /// <summary>Detenido / sin sesión activa.</summary>
    Idle,
    /// <summary>Bloque de concentración en curso.</summary>
    Focus,
    /// <summary>Descanso corto.</summary>
    ShortBreak,
    /// <summary>Descanso largo.</summary>
    LongBreak
}
