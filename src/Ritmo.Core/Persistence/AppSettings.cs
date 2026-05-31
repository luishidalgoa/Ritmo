using Ritmo.Core.Focus;
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

    /// <summary>Sesiones provisionales (con fecha) que se superponen a la semana. #103</summary>
    public IReadOnlyList<OneOffSession> OneOffSessions { get; init; } = [];

    public PomodoroConfig Pomodoro { get; init; } = PomodoroConfig.DeepWork;

    /// <summary>Ritmos Pomodoro propios del usuario (los de por defecto van en código). #96</summary>
    public IReadOnlyList<PomodoroRhythm> Rhythms { get; init; } = [];

    /// <summary>Notas fijadas por el usuario.</summary>
    public IReadOnlyList<StudyNote> Notes { get; init; } = [];

    /// <summary>Preferencias de visualización del horario.</summary>
    public ScheduleViewConfig ViewConfig { get; init; } = new();

    /// <summary>Entornos de concentración definidos por el usuario.</summary>
    public IReadOnlyList<FocusEnvironment> FocusEnvironments { get; init; } = [];

    /// <summary>Id del entorno usado por defecto al iniciar focus (o null).</summary>
    public string? DefaultFocusEnvironmentId { get; init; }

    /// <summary>Conexión GLOBAL a Navidrome (servidor + usuario). La contraseña va en
    /// el almacén seguro del SO, nunca aquí. Cada entorno solo elige la playlist. #107</summary>
    public string? NavidromeServerUrl { get; init; }
    public string? NavidromeUser { get; init; }

    /// <summary>
    /// Notificaciones push al móvil vía ntfy (#122). OPT-IN: por defecto desactivado.
    /// Cuando está activo, cada aviso del horario (además del toast de Windows) se
    /// publica en {NtfyServerUrl}/{NtfyTopic} y el móvil suscrito al topic lo recibe.
    /// El topic actúa como secreto compartido (quien lo conoce, recibe los avisos).
    /// </summary>
    public bool NtfyEnabled { get; init; }
    public string? NtfyServerUrl { get; init; }
    public string? NtfyTopic { get; init; }

    /// <summary>
    /// Última versión de la app cuyas «Novedades» vio el usuario (p. ej. "1.0.1.0").
    /// Null = nunca las ha visto. Al actualizar la app, si la versión actual es mayor
    /// que esta, se activa el botón «Novedades» con el carrusel. #updates
    /// </summary>
    public string? LastSeenVersion { get; init; }

    /// <summary>Suscripciones a calendarios externos por enlace ICS (lectura). #112</summary>
    public IReadOnlyList<CalendarFeed> CalendarFeeds { get; init; } = [];

    /// <summary>
    /// Prioridades elegidas ante solapamientos horario↔calendario (#114): por cada
    /// evento en conflicto, qué lado se prioriza. Solo afecta a cómo se pinta.
    /// </summary>
    public IReadOnlyList<OverlapPriority> OverlapPriorities { get; init; } = [];

    /// <summary>
    /// Mapeo opcional tipo de bloque → id de entorno, para asociar automáticamente
    /// (p. ej. un bloque "Simulacro" usa el entorno "Simulacro").
    /// </summary>
    public IReadOnlyDictionary<StudyKind, string> EnvironmentByKind { get; init; }
        = new Dictionary<StudyKind, string>();

    public static AppSettings Default => new();

    /// <summary>
    /// Resuelve qué entorno usar para un tipo de bloque: primero el mapeo por tipo,
    /// luego el por defecto, y si nada aplica, null.
    /// </summary>
    public FocusEnvironment? ResolveEnvironment(StudyKind kind)
    {
        if (EnvironmentByKind.TryGetValue(kind, out var id))
        {
            var byKind = FocusEnvironments.FirstOrDefault(e => e.Id == id);
            if (byKind is not null) return byKind;
        }
        return FocusEnvironments.FirstOrDefault(e => e.Id == DefaultFocusEnvironmentId);
    }
}
