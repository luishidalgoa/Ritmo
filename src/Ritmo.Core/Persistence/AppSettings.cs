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

    /// <summary>
    /// Categorías de bloque definibles por el usuario (#83). Reemplazan al antiguo enum
    /// fijo de tipos. De fábrica = set neutral; los usuarios existentes las reciben por
    /// migración (ver <c>CategoryMigration</c>). Siempre incluye «Otro» y «Por definir».
    /// </summary>
    public IReadOnlyList<BlockCategory> Categories { get; init; } = CategoryDefaults.Neutral();

    /// <summary>Si el usuario ya pasó el onboarding (selector de plantillas). #83</summary>
    public bool OnboardingCompleted { get; init; }

    /// <summary>Modo descanso MANUAL (#135): pausa los avisos del horario «ahora» hasta apagarlo.</summary>
    public bool RestActive { get; init; }

    /// <summary>Periodos de descanso PROGRAMADOS (#135): pausan los avisos del horario en sus fechas
    /// (p. ej. vacaciones). El horario sigue viéndose; solo se silencian los avisos.</summary>
    public IReadOnlyList<RestPeriod> RestPeriods { get; init; } = [];

    /// <summary>Seguimiento laboral (#84): tarifa por hora de cada entorno/proyecto (id → €/h).</summary>
    public IReadOnlyDictionary<string, decimal> EnvironmentRates { get; init; }
        = new Dictionary<string, decimal>();

    /// <summary>Seguimiento laboral (#84): registro MANUAL de horas trabajadas por día y entorno.</summary>
    public IReadOnlyList<WorkLogEntry> WorkLog { get; init; } = [];

    /// <summary>Seguimiento laboral (#84 V2): objetivo de horas/mes por entorno (id → horas). 0/ausente = sin objetivo.</summary>
    public IReadOnlyDictionary<string, double> EnvironmentGoals { get; init; }
        = new Dictionary<string, double>();

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
    /// Mapeo opcional categoría de bloque (id) → id de entorno, para asociar automáticamente
    /// (p. ej. un bloque "Reunión" usa el entorno "Reunión"). #70
    /// </summary>
    public IReadOnlyDictionary<string, string> EnvironmentByKind { get; init; }
        = new Dictionary<string, string>();

    public static AppSettings Default => new();

    /// <summary>
    /// Resuelve qué entorno usar para una categoría de bloque: primero el mapeo por categoría,
    /// luego el por defecto, y si nada aplica, null.
    /// </summary>
    public FocusEnvironment? ResolveEnvironment(string categoryId)
    {
        if (!string.IsNullOrEmpty(categoryId) && EnvironmentByKind.TryGetValue(categoryId, out var id))
        {
            var byKind = FocusEnvironments.FirstOrDefault(e => e.Id == id);
            if (byKind is not null) return byKind;
        }
        return FocusEnvironments.FirstOrDefault(e => e.Id == DefaultFocusEnvironmentId);
    }

    /// <summary>La categoría con ese id, o null si no existe. #83</summary>
    public BlockCategory? Category(string? id)
        => id is null ? null : Categories.FirstOrDefault(c => string.Equals(c.Id, id, System.StringComparison.OrdinalIgnoreCase));

    /// <summary>La categoría con ese id; si no existe, cae a «Otro» (o una gris si faltara). #83</summary>
    public BlockCategory CategoryOrFallback(string? id)
        => Category(id)
           ?? Category(CategoryIds.Other)
           ?? new BlockCategory { Id = CategoryIds.Other, Name = "Otro", ColorHex = LegacyCategories.UnknownColor, TextColorHex = LegacyCategories.UnknownTextColor, IsSystem = true };

    /// <summary>Nombre visible de la categoría (o «Otro» si no existe). #83</summary>
    public string CategoryName(string? id) => CategoryOrFallback(id).Name;

    /// <summary>¿La categoría dispara concentración? (false si no existe). #83</summary>
    public bool IsFocusCategory(string? id) => Category(id)?.IsFocus ?? false;

    /// <summary>Ids de las categorías que disparan concentración (para el planificador). #83</summary>
    public IReadOnlySet<string> FocusCategoryIds()
        => Categories.Where(c => c.IsFocus).Select(c => c.Id).ToHashSet(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// ¿Está en modo descanso esa fecha? (#135) Lo está si el descanso manual está activo o si
    /// algún periodo programado cubre esa fecha. En descanso, el horario NO lanza avisos.
    /// </summary>
    public bool IsRestingOn(System.DateOnly date) => RestActive || RestPeriods.Any(p => p.Covers(date));
}
