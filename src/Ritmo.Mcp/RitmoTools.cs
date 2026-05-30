using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;
using Ritmo.Core.Commands;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;

namespace Ritmo.Mcp;

/// <summary>
/// Herramientas MCP que una IA (Claude, etc.) puede invocar para configurar
/// Ritmo. Cada método envuelve la capa de comandos del núcleo (ConfigurationService),
/// que valida y persiste. Las descripciones guían a la IA sobre cuándo usarlas.
/// </summary>
[McpServerToolType]
public sealed class RitmoTools
{
    private readonly ConfigurationService _config;

    public RitmoTools(ConfigurationService config) => _config = config;

    [McpServerTool(Name = "get_status")]
    [Description("Devuelve un resumen del estado de Ritmo: número de fases y sus nombres, entornos de concentración, entorno por defecto y número de notas. Úsalo primero para saber qué hay configurado.")]
    public string GetStatus()
    {
        var s = _config.GetStatus();
        var phases = s.PhaseNames.Count == 0 ? "(ninguna)" : string.Join(", ", s.PhaseNames);
        var envs = s.EnvironmentNames.Count == 0 ? "(ninguno)" : string.Join(", ", s.EnvironmentNames);
        return $"Fases: {s.PhaseCount} [{phases}]. Entornos: {s.EnvironmentCount} [{envs}]. " +
               $"Entorno por defecto: {s.DefaultEnvironmentId ?? "(ninguno)"}. Notas: {s.NoteCount}.";
    }

    [McpServerTool(Name = "add_phase")]
    [Description("Crea una fase temporal del horario (un periodo con su propio horario semanal). Fechas en formato yyyy-MM-dd. validTo puede ir vacío para una fase indefinida.")]
    public string AddPhase(
        [Description("Nombre de la fase, p. ej. 'Fase 1'")] string name,
        [Description("Fecha de inicio (yyyy-MM-dd)")] string validFrom,
        [Description("Fecha de fin (yyyy-MM-dd) o vacío si es indefinida")] string? validTo = null)
    {
        if (!TryDate(validFrom, out var from))
            return Err($"Fecha de inicio inválida: '{validFrom}' (usa yyyy-MM-dd).");
        DateOnly? to = null;
        if (!string.IsNullOrWhiteSpace(validTo))
        {
            if (!TryDate(validTo, out var parsed)) return Err($"Fecha de fin inválida: '{validTo}'.");
            to = parsed;
        }
        return Report(_config.AddPhase(name, from, to));
    }

    [McpServerTool(Name = "add_session")]
    [Description("Añade una sesión de estudio a una fase existente (por nombre). day en inglés (Monday..Sunday). start en HH:mm. durationMinutes en minutos. kind: Tecnico, Legislacion, Ingles, Tests, Simulacro, Descanso, PorDefinir, Otro. preAlertsMinutes: lista de minutos de aviso previo separados por coma (ej. '60,10'). tentative=true para bloque provisional.")]
    public string AddSession(
        [Description("Nombre de la fase destino")] string phaseName,
        [Description("Título de la sesión")] string title,
        [Description("Día de la semana en inglés: Monday..Sunday")] string day,
        [Description("Hora de inicio HH:mm")] string start,
        [Description("Duración en minutos")] int durationMinutes,
        [Description("Tipo: Tecnico, Legislacion, Ingles, Tests, Simulacro, Descanso, PorDefinir, Otro")] string kind = "Otro",
        [Description("Minutos de avisos previos separados por coma, ej. '60,10'")] string? preAlertsMinutes = null,
        [Description("true si el bloque es provisional/tentativo")] bool tentative = false)
    {
        if (!Enum.TryParse<DayOfWeek>(day, ignoreCase: true, out var dow))
            return Err($"Día inválido: '{day}' (usa Monday..Sunday).");
        if (!TimeOnly.TryParseExact(start, "HH\\:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
            return Err($"Hora inválida: '{start}' (usa HH:mm).");
        if (durationMinutes <= 0)
            return Err("La duración debe ser mayor que 0.");
        var theKind = Enum.TryParse<StudyKind>(kind, ignoreCase: true, out var k) ? k : StudyKind.Otro;

        var alerts = new List<PreAlert>();
        if (!string.IsNullOrWhiteSpace(preAlertsMinutes))
        {
            foreach (var part in preAlertsMinutes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (int.TryParse(part, out var m)) alerts.Add(new PreAlert(m));
        }

        var session = new StudySession
        {
            Title = title, Day = dow, Start = startTime,
            Duration = TimeSpan.FromMinutes(durationMinutes), Kind = theKind,
            PreAlerts = alerts, IsTentative = tentative
        };
        return Report(_config.AddSession(phaseName, session));
    }

    [McpServerTool(Name = "upsert_focus_environment")]
    [Description("Crea o actualiza un entorno de concentración (qué pasa al entrar en modo focus). Listas separadas por coma. id estable; si ya existe, se reemplaza.")]
    public string UpsertFocusEnvironment(
        [Description("Id estable del entorno, p. ej. 'deep'")] string id,
        [Description("Nombre visible, p. ej. 'Estudio profundo'")] string name,
        [Description("Activar No molestar (true/false)")] bool doNotDisturb = true,
        [Description("Webs a bloquear, separadas por coma (ej. 'youtube.com,reddit.com')")] string? blockedWebsites = null,
        [Description("Apps a cerrar, separadas por coma (ej. 'Discord,Steam')")] string? appsToClose = null,
        [Description("Apps a silenciar, separadas por coma")] string? appsToMute = null,
        [Description("Abrir los enlaces del entorno en una ventana nueva del navegador por defecto (true/false)")] bool openLinksInBrowser = false,
        [Description("Nombre de la app de música a lanzar, p. ej. 'Spotify' (vacío para ninguna)")] string? musicName = null,
        [Description("Ejecutable o URI de la música, p. ej. 'spotify:'")] string? musicTarget = null)
    {
        var env = new FocusEnvironment
        {
            Id = id, Name = name, EnableDoNotDisturb = doNotDisturb,
            OpenLinksInBrowser = openLinksInBrowser,
            BlockedWebsites = Split(blockedWebsites),
            AppsToClose = Split(appsToClose),
            AppsToMute = Split(appsToMute),
            Music = string.IsNullOrWhiteSpace(musicName) || string.IsNullOrWhiteSpace(musicTarget)
                ? null
                : new MusicLauncher { Name = musicName!, Target = musicTarget!, AutoPlay = true }
        };
        return Report(_config.UpsertEnvironment(env));
    }

    [McpServerTool(Name = "set_default_environment")]
    [Description("Fija el entorno de concentración por defecto, por su id. El entorno debe existir.")]
    public string SetDefaultEnvironment([Description("Id del entorno")] string environmentId)
        => Report(_config.SetDefaultEnvironment(environmentId));

    [McpServerTool(Name = "map_environment_to_kind")]
    [Description("Asocia un tipo de bloque (Tecnico, Simulacro...) a un entorno, para que ese tipo use ese entorno automáticamente al iniciar focus.")]
    public string MapEnvironmentToKind(
        [Description("Tipo: Tecnico, Legislacion, Ingles, Tests, Simulacro, Descanso, PorDefinir, Otro")] string kind,
        [Description("Id del entorno")] string environmentId)
    {
        if (!Enum.TryParse<StudyKind>(kind, ignoreCase: true, out var k))
            return Err($"Tipo inválido: '{kind}'.");
        return Report(_config.MapEnvironmentToKind(k, environmentId));
    }

    // ---------- helpers ----------
    private static bool TryDate(string s, out DateOnly d) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

    private static List<string> Split(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? []
        : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string Report(CommandResult r) =>
        r.Success ? $"OK: {r.Message}" : $"ERROR: {string.Join(" | ", r.Errors.DefaultIfEmpty(r.Message))}";

    private static string Err(string msg) => $"ERROR: {msg}";
}
