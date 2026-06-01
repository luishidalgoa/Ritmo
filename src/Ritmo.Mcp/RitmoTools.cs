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
/// que valida y persiste. Cubren TODA la superficie de configuración de la app, de
/// modo que la IA tiene control total: fases y sesiones del horario, sesiones
/// provisionales, Pomodoro y ritmos, rango horario, notas, atajos, entornos de
/// concentración (con apps, enlaces, tareas y perfiles por tipo de sesión),
/// calendarios, prioridades de solapamiento, música (Navidrome) e importar/exportar.
///
/// Flujo recomendado para la IA: llamar primero a <c>get_config</c> para VER el
/// estado completo (con los ids e índices que usan las herramientas de editar/borrar)
/// y a <c>list_known_apps</c> para conocer los nombres de proceso válidos.
/// </summary>
[McpServerToolType]
public sealed class RitmoTools
{
    private readonly ConfigurationService _config;

    public RitmoTools(ConfigurationService config) => _config = config;

    // Pista para el parámetro de categoría, reutilizada en las descripciones (#83).
    private const string KindList = "id de categoría (usa list_categories para ver las disponibles; p. ej. 'Otro')";

    // ==================== LECTURA ====================

    [McpServerTool(Name = "get_status")]
    [Description("Resumen corto del estado: nº de fases y nombres, entornos, entorno por defecto y nº de notas. Para ver TODO el detalle (ids, índices, apps, enlaces...) usa get_config.")]
    public string GetStatus()
    {
        var s = _config.GetStatus();
        var phases = s.PhaseNames.Count == 0 ? "(ninguna)" : string.Join(", ", s.PhaseNames);
        var envs = s.EnvironmentNames.Count == 0 ? "(ninguno)" : string.Join(", ", s.EnvironmentNames);
        return $"Fases: {s.PhaseCount} [{phases}]. Entornos: {s.EnvironmentCount} [{envs}]. " +
               $"Entorno por defecto: {s.DefaultEnvironmentId ?? "(ninguno)"}. Notas: {s.NoteCount}.";
    }

    [McpServerTool(Name = "get_config")]
    [Description("Devuelve TODA la configuración de Ritmo como JSON: fases con sus sesiones (el índice de cada sesión es su posición en el array, empezando en 0), sesiones provisionales (con id), Pomodoro y ritmos (con id), rango horario, notas (con id), entornos de concentración (id, apps a abrir/cerrar/silenciar, enlaces, tareas con id, perfiles por sesión), calendarios (con id), prioridades de solapamiento (con eventKey), Navidrome y mapeo tipo→entorno. Úsalo SIEMPRE antes de editar o borrar para conocer ids e índices. No incluye contraseñas.")]
    public string GetConfig() => _config.ExportJson();

    [McpServerTool(Name = "list_known_apps")]
    [Description("Catálogo de apps conocidas para configurar entornos. Para 'apps a cerrar/silenciar' usa el nombre de proceso; para 'apps a abrir' usa el destino de lanzamiento (o el nombre de proceso si no hay destino).")]
    public string ListKnownApps()
    {
        var lines = new List<string>();
        foreach (var (cat, apps) in KnownApps.ByCategory())
        {
            lines.Add($"## {KnownApps.Label(cat)}");
            foreach (var a in apps)
            {
                var open = string.IsNullOrWhiteSpace(a.LaunchTarget) ? a.ProcessName : a.LaunchTarget;
                lines.Add($"- {a.Name} (proceso: {a.ProcessName}; abrir: {open})");
            }
        }
        return string.Join("\n", lines);
    }

    // ==================== FASES ====================

    [McpServerTool(Name = "add_phase")]
    [Description("Crea una fase temporal del horario (un periodo con su propio horario semanal). Fechas en yyyy-MM-dd. validTo puede ir vacío para una fase indefinida.")]
    public string AddPhase(
        [Description("Nombre de la fase, p. ej. 'Fase 1'")] string name,
        [Description("Fecha de inicio (yyyy-MM-dd)")] string validFrom,
        [Description("Fecha de fin (yyyy-MM-dd) o vacío si es indefinida")] string? validTo = null)
    {
        if (!TryDate(validFrom, out var from)) return Err($"Fecha de inicio inválida: '{validFrom}' (usa yyyy-MM-dd).");
        if (!TryOptionalDate(validTo, out var to)) return Err($"Fecha de fin inválida: '{validTo}'.");
        return Report(_config.AddPhase(name, from, to));
    }

    [McpServerTool(Name = "update_phase")]
    [Description("Renombra y/o cambia la vigencia de una fase existente (localizada por su nombre actual). Fechas en yyyy-MM-dd; validTo vacío = indefinida.")]
    public string UpdatePhase(
        [Description("Nombre actual de la fase")] string name,
        [Description("Nuevo nombre (puede ser igual al actual)")] string newName,
        [Description("Nueva fecha de inicio (yyyy-MM-dd)")] string validFrom,
        [Description("Nueva fecha de fin (yyyy-MM-dd) o vacío")] string? validTo = null)
    {
        if (!TryDate(validFrom, out var from)) return Err($"Fecha de inicio inválida: '{validFrom}'.");
        if (!TryOptionalDate(validTo, out var to)) return Err($"Fecha de fin inválida: '{validTo}'.");
        return Report(_config.UpdatePhase(name, newName, from, to));
    }

    [McpServerTool(Name = "remove_phase")]
    [Description("Elimina una fase del plan (por nombre). Debe quedar al menos una fase.")]
    public string RemovePhase([Description("Nombre de la fase a eliminar")] string name)
        => Report(_config.RemovePhase(name));

    [McpServerTool(Name = "duplicate_phase")]
    [Description("Duplica una fase existente: crea otra con NUEVO nombre y vigencia copiando su horario semanal completo. Útil para preparar la siguiente fase a partir de la actual. Fechas en yyyy-MM-dd; validTo vacío = indefinida.")]
    public string DuplicatePhase(
        [Description("Nombre de la fase a copiar")] string sourceName,
        [Description("Nombre de la fase nueva")] string newName,
        [Description("Fecha de inicio de la nueva fase (yyyy-MM-dd)")] string validFrom,
        [Description("Fecha de fin (yyyy-MM-dd) o vacío si es indefinida")] string? validTo = null)
    {
        if (!TryDate(validFrom, out var from)) return Err($"Fecha de inicio inválida: '{validFrom}' (usa yyyy-MM-dd).");
        if (!TryOptionalDate(validTo, out var to)) return Err($"Fecha de fin inválida: '{validTo}'.");
        return Report(_config.DuplicatePhase(sourceName, newName, from, to));
    }

    // ==================== SESIONES (en una fase) ====================

    [McpServerTool(Name = "add_session")]
    [Description("Añade una sesión recurrente a una fase existente (por nombre). day en inglés (Monday..Sunday). start en HH:mm. preAlertsMinutes: minutos de aviso separados por coma (ej. '60,10'). tentative=true para bloque provisional/sin contenido decidido.")]
    public string AddSession(
        [Description("Nombre de la fase destino")] string phaseName,
        [Description("Título de la sesión")] string title,
        [Description("Día de la semana en inglés: Monday..Sunday")] string day,
        [Description("Hora de inicio HH:mm")] string start,
        [Description("Duración en minutos")] int durationMinutes,
        [Description("Tipo: " + KindList)] string kind = "Otro",
        [Description("Minutos de avisos previos separados por coma, ej. '60,10'")] string? preAlertsMinutes = null,
        [Description("true si el bloque es provisional/tentativo")] bool tentative = false)
    {
        if (!Enum.TryParse<DayOfWeek>(day, ignoreCase: true, out var dow))
            return Err($"Día inválido: '{day}' (usa Monday..Sunday).");
        if (!TryTime(start, out var startTime)) return Err($"Hora inválida: '{start}' (usa HH:mm).");
        if (durationMinutes <= 0) return Err("La duración debe ser mayor que 0.");

        var session = new StudySession
        {
            Title = title, Day = dow, Start = startTime,
            Duration = TimeSpan.FromMinutes(durationMinutes), CategoryId = ParseKind(kind),
            PreAlerts = ParseAlerts(preAlertsMinutes), IsTentative = tentative
        };
        return Report(_config.AddSession(phaseName, session));
    }

    [McpServerTool(Name = "update_session")]
    [Description("Reemplaza la sesión en el índice dado de una fase (el índice es su posición en el array 'sessions' de la fase en get_config, empezando en 0). Debes pasar TODOS los campos: los no enviados se reemplazan por el valor por defecto.")]
    public string UpdateSession(
        [Description("Nombre de la fase")] string phaseName,
        [Description("Índice de la sesión (0 = primera)")] int index,
        [Description("Título de la sesión")] string title,
        [Description("Día de la semana en inglés: Monday..Sunday")] string day,
        [Description("Hora de inicio HH:mm")] string start,
        [Description("Duración en minutos")] int durationMinutes,
        [Description("Tipo: " + KindList)] string kind = "Otro",
        [Description("Minutos de avisos previos separados por coma, ej. '60,10'")] string? preAlertsMinutes = null,
        [Description("true si el bloque es provisional/tentativo")] bool tentative = false)
    {
        if (!Enum.TryParse<DayOfWeek>(day, ignoreCase: true, out var dow))
            return Err($"Día inválido: '{day}' (usa Monday..Sunday).");
        if (!TryTime(start, out var startTime)) return Err($"Hora inválida: '{start}' (usa HH:mm).");
        if (durationMinutes <= 0) return Err("La duración debe ser mayor que 0.");

        var session = new StudySession
        {
            Title = title, Day = dow, Start = startTime,
            Duration = TimeSpan.FromMinutes(durationMinutes), CategoryId = ParseKind(kind),
            PreAlerts = ParseAlerts(preAlertsMinutes), IsTentative = tentative
        };
        return Report(_config.UpdateSession(phaseName, index, session));
    }

    [McpServerTool(Name = "remove_session")]
    [Description("Elimina la sesión en el índice dado de una fase (índice = posición en el array 'sessions' de la fase, empezando en 0).")]
    public string RemoveSession(
        [Description("Nombre de la fase")] string phaseName,
        [Description("Índice de la sesión (0 = primera)")] int index)
        => Report(_config.RemoveSession(phaseName, index));

    // ==================== SESIONES PROVISIONALES (con fecha) ====================

    [McpServerTool(Name = "add_one_off_session")]
    [Description("Añade una sesión provisional/extraordinaria en una FECHA concreta (no recurrente; se superpone a la semana de esa fecha). Devuelve su id. date en yyyy-MM-dd, start en HH:mm.")]
    public string AddOneOffSession(
        [Description("Fecha (yyyy-MM-dd)")] string date,
        [Description("Título")] string title,
        [Description("Hora de inicio HH:mm")] string start,
        [Description("Duración en minutos")] int durationMinutes,
        [Description("Tipo: " + KindList)] string kind = "Otro",
        [Description("Minutos de avisos previos separados por coma, ej. '60,10'")] string? preAlertsMinutes = null,
        [Description("true si es provisional/tentativo")] bool tentative = false)
    {
        if (!TryDate(date, out var d)) return Err($"Fecha inválida: '{date}' (usa yyyy-MM-dd).");
        if (!TryTime(start, out var startTime)) return Err($"Hora inválida: '{start}' (usa HH:mm).");
        if (durationMinutes <= 0) return Err("La duración debe ser mayor que 0.");
        return Report(_config.AddOneOffSession(d, title, startTime, TimeSpan.FromMinutes(durationMinutes),
            ParseKind(kind), ParseAlerts(preAlertsMinutes), tentative));
    }

    [McpServerTool(Name = "remove_one_off_session")]
    [Description("Elimina una sesión provisional por su id (campo 'id' de oneOffSessions en get_config).")]
    public string RemoveOneOffSession([Description("Id de la sesión provisional")] string id)
        => Report(_config.RemoveOneOffSession(id));

    // ==================== POMODORO Y RITMOS ====================

    [McpServerTool(Name = "set_pomodoro")]
    [Description("Configura el Pomodoro por defecto de la app (duraciones en minutos y cada cuántos focos toca el descanso largo).")]
    public string SetPomodoro(
        [Description("Minutos de concentración (>0)")] int focusMinutes,
        [Description("Minutos de descanso corto")] int shortBreakMinutes,
        [Description("Minutos de descanso largo")] int longBreakMinutes,
        [Description("Focos por descanso largo (>=1)")] int focusesPerLongBreak)
        => Report(_config.SetPomodoro(focusMinutes, shortBreakMinutes, longBreakMinutes, focusesPerLongBreak));

    [McpServerTool(Name = "add_rhythm")]
    [Description("Crea un ritmo Pomodoro propio (con nombre) que luego se puede asignar a un entorno. Devuelve su id.")]
    public string AddRhythm(
        [Description("Nombre del ritmo")] string name,
        [Description("Minutos de concentración (>0)")] int focusMinutes,
        [Description("Minutos de descanso corto")] int shortBreakMinutes,
        [Description("Minutos de descanso largo")] int longBreakMinutes,
        [Description("Focos por descanso largo (>=1)")] int focusesPerLongBreak)
        => Report(_config.AddRhythm(name, focusMinutes, shortBreakMinutes, longBreakMinutes, focusesPerLongBreak));

    [McpServerTool(Name = "update_rhythm")]
    [Description("Edita un ritmo Pomodoro propio (por id). No se pueden editar los de por defecto ('classic', 'deepwork').")]
    public string UpdateRhythm(
        [Description("Id del ritmo")] string id,
        [Description("Nombre del ritmo")] string name,
        [Description("Minutos de concentración (>0)")] int focusMinutes,
        [Description("Minutos de descanso corto")] int shortBreakMinutes,
        [Description("Minutos de descanso largo")] int longBreakMinutes,
        [Description("Focos por descanso largo (>=1)")] int focusesPerLongBreak)
        => Report(_config.UpdateRhythm(id, name, focusMinutes, shortBreakMinutes, longBreakMinutes, focusesPerLongBreak));

    [McpServerTool(Name = "remove_rhythm")]
    [Description("Elimina un ritmo Pomodoro propio (por id).")]
    public string RemoveRhythm([Description("Id del ritmo")] string id)
        => Report(_config.RemoveRhythm(id));

    // ==================== VISTA DEL HORARIO ====================

    [McpServerTool(Name = "set_view_hours")]
    [Description("Fija el rango horario visible de la rejilla del horario (primera y última hora mostrada). Horas en HH:mm; fin debe ser posterior al inicio.")]
    public string SetViewHours(
        [Description("Hora de inicio del día visible, HH:mm")] string dayStart,
        [Description("Hora de fin del día visible, HH:mm")] string dayEnd)
    {
        if (!TryTime(dayStart, out var from)) return Err($"Hora inválida: '{dayStart}' (usa HH:mm).");
        if (!TryTime(dayEnd, out var to)) return Err($"Hora inválida: '{dayEnd}' (usa HH:mm).");
        return Report(_config.SetViewHours(from, to));
    }

    [McpServerTool(Name = "set_view_granularity")]
    [Description("Fija la granularidad de la rejilla de fondo del horario: 60 (por defecto), 30 o 15 minutos por línea. Solo afecta a las líneas-guía; los bloques se posicionan por su minuto real.")]
    public string SetViewGranularity(
        [Description("Minutos por línea de la rejilla: 60, 30 o 15")] int minutes)
        => Report(_config.SetGranularity(minutes));

    [McpServerTool(Name = "set_day_preview")]
    [Description("Activa o desactiva la vista previa del día al iniciar concentración: si está activa, al arrancar el foco se muestra un resumen de los bloques de hoy.")]
    public string SetDayPreview(
        [Description("true = mostrar la vista previa al iniciar foco; false = no mostrarla")] bool enabled)
        => Report(_config.SetShowDayPreviewOnFocusStart(enabled));

    [McpServerTool(Name = "set_default_prealert")]
    [Description("Fija el aviso previo por defecto (minutos) con que se pre-rellena una sesión NUEVA. 0 = sin aviso. Rango 0..1440. No cambia las sesiones ya creadas.")]
    public string SetDefaultPreAlert(
        [Description("Minutos de aviso previo por defecto (0 = ninguno; máx. 1440)")] int minutes)
        => Report(_config.SetDefaultPreAlert(minutes));

    [McpServerTool(Name = "set_rest_active")]
    [Description("Activa/desactiva el modo descanso MANUAL: mientras está activo, el horario NO lanza avisos (útil para una pausa). El horario se sigue viendo; no borra nada.")]
    public string SetRestActive(
        [Description("true = en descanso (avisos en pausa); false = normal")] bool active)
        => Report(_config.SetRestActive(active));

    [McpServerTool(Name = "add_rest_period")]
    [Description("Programa un periodo de descanso (p. ej. vacaciones): durante esas fechas el horario no lanza avisos. Fechas en yyyy-MM-dd; fin >= inicio. label opcional.")]
    public string AddRestPeriod(
        [Description("Fecha de inicio (yyyy-MM-dd)")] string from,
        [Description("Fecha de fin INCLUSIVE (yyyy-MM-dd)")] string to,
        [Description("Etiqueta opcional, p. ej. 'Vacaciones'")] string? label = null)
    {
        if (!TryDate(from, out var f)) return Err($"Fecha de inicio inválida: '{from}' (usa yyyy-MM-dd).");
        if (!TryDate(to, out var t)) return Err($"Fecha de fin inválida: '{to}'.");
        return Report(_config.AddRestPeriod(f, t, label ?? ""));
    }

    [McpServerTool(Name = "remove_rest_period")]
    [Description("Elimina un periodo de descanso programado por su id (ver get_config).")]
    public string RemoveRestPeriod([Description("Id del periodo de descanso")] string id)
        => Report(_config.RemoveRestPeriod(id));

    [McpServerTool(Name = "add_work_project")]
    [Description("Seguimiento laboral: crea un PROYECTO/cliente con tarifa por hora y objetivo mensual opcionales. Devuelve su id. currencyCode p. ej. EUR/USD/GBP.")]
    public string AddWorkProject(
        [Description("Nombre del proyecto/cliente")] string name,
        [Description("Tarifa por hora (>= 0; 0 = sin tarifa)")] double rate = 0,
        [Description("Objetivo de horas al mes (>= 0; 0 = sin objetivo)")] double monthlyGoalHours = 0,
        [Description("Color #RRGGBB")] string colorHex = "#1E88E5",
        [Description("Código de moneda ISO (EUR, USD, GBP…)")] string currencyCode = "EUR")
        => Report(_config.AddWorkProject(name, (decimal)rate, monthlyGoalHours, colorHex, currencyCode));

    [McpServerTool(Name = "update_work_project")]
    [Description("Seguimiento laboral: edita un proyecto (nombre/tarifa/objetivo/color/moneda/archivado). Solo cambia lo que envíes; deja vacío/nulo lo demás.")]
    public string UpdateWorkProject(
        [Description("Id del proyecto")] string id,
        [Description("Nuevo nombre (vacío = sin cambio)")] string? name = null,
        [Description("Nueva tarifa (negativo = sin cambio)")] double rate = -1,
        [Description("Nuevo objetivo h/mes (negativo = sin cambio)")] double monthlyGoalHours = -1,
        [Description("Nuevo color #RRGGBB (vacío = sin cambio)")] string? colorHex = null,
        [Description("Nueva moneda (vacío = sin cambio)")] string? currencyCode = null)
        => Report(_config.UpdateWorkProject(id,
            string.IsNullOrWhiteSpace(name) ? null : name,
            rate < 0 ? null : (decimal)rate,
            monthlyGoalHours < 0 ? null : monthlyGoalHours,
            string.IsNullOrWhiteSpace(colorHex) ? null : colorHex,
            string.IsNullOrWhiteSpace(currencyCode) ? null : currencyCode));

    [McpServerTool(Name = "remove_work_project")]
    [Description("Seguimiento laboral: elimina un proyecto y TODAS sus anotaciones de horas (por id).")]
    public string RemoveWorkProject([Description("Id del proyecto")] string id)
        => Report(_config.RemoveWorkProject(id));

    [McpServerTool(Name = "log_work_hours")]
    [Description("Seguimiento laboral: anota horas trabajadas en un PROYECTO un día (acumulativo). projectId de add_work_project/get_config. Fecha yyyy-MM-dd; horas > 0. note opcional.")]
    public string LogWorkHours(
        [Description("Id del proyecto")] string projectId,
        [Description("Fecha (yyyy-MM-dd)")] string date,
        [Description("Horas trabajadas (> 0)")] double hours,
        [Description("Nota opcional (qué se hizo)")] string note = "")
    {
        if (!TryDate(date, out var d)) return Err($"Fecha inválida: '{date}' (usa yyyy-MM-dd).");
        return Report(_config.AddWorkHours(projectId, d, hours, note));
    }

    [McpServerTool(Name = "remove_work_log_entry")]
    [Description("Seguimiento laboral: elimina una anotación de horas por su id (ver get_config).")]
    public string RemoveWorkLogEntry([Description("Id de la anotación de horas")] string id)
        => Report(_config.RemoveWorkLogEntry(id));

    [McpServerTool(Name = "set_kind_color")]
    [Description("Fija el color de fondo de un tipo de bloque en la rejilla del horario. hex en formato #RRGGBB; deja hex vacío para volver al color por defecto de ese tipo.")]
    public string SetKindColor(
        [Description("Tipo: " + KindList)] string kind,
        [Description("Color #RRGGBB (vacío = restablecer al por defecto)")] string? hex = null)
    {
        return Report(_config.SetKindColor(kind, hex));
    }

    // ==================== NOTAS ====================

    [McpServerTool(Name = "add_note")]
    [Description("Añade una nota fijada (el contenido admite markdown). Si pasas sessionTitle, la nota es un 'post-it' de ese tipo de sesión. Devuelve su id.")]
    public string AddNote(
        [Description("Título de la nota")] string title,
        [Description("Contenido en markdown")] string content,
        [Description("Color de acento hex '#RRGGBB' (opcional)")] string? accentColor = null,
        [Description("Título de la sesión a la que se asocia (opcional; vacío = nota suelta)")] string? sessionTitle = null)
        => Report(_config.AddNote(title, content, accentColor, sessionTitle));

    [McpServerTool(Name = "update_note")]
    [Description("Edita el título/contenido/color de una nota existente (por id).")]
    public string UpdateNote(
        [Description("Id de la nota")] string id,
        [Description("Título")] string title,
        [Description("Contenido en markdown")] string content,
        [Description("Color de acento hex '#RRGGBB' (opcional)")] string? accentColor = null)
        => Report(_config.UpdateNote(id, title, content, accentColor));

    [McpServerTool(Name = "remove_note")]
    [Description("Elimina una nota por su id.")]
    public string RemoveNote([Description("Id de la nota")] string id)
        => Report(_config.RemoveNote(id));

    // ==================== ATAJOS GLOBALES ====================

    [McpServerTool(Name = "add_shortcut")]
    [Description("Añade un enlace-atajo global (título + URL) accesible desde el horario.")]
    public string AddShortcut(
        [Description("Título del enlace")] string title,
        [Description("URL")] string url)
        => Report(_config.AddShortcut(title, url));

    [McpServerTool(Name = "remove_shortcut")]
    [Description("Elimina el enlace-atajo global en el índice dado (posición en viewConfig.shortcuts, empezando en 0).")]
    public string RemoveShortcut([Description("Índice del enlace (0 = primero)")] int index)
        => Report(_config.RemoveShortcut(index));

    // ==================== ENTORNOS DE CONCENTRACIÓN ====================

    [McpServerTool(Name = "upsert_focus_environment")]
    [Description("Crea o actualiza un entorno de concentración (por id). Para actualizar uno existente, los parámetros que dejes vacíos/null se MANTIENEN como están (no se borran), y los enlaces, tareas y perfiles por sesión SIEMPRE se conservan (se gestionan con sus propias herramientas). Las listas (csv) se reemplazan por completo si las envías; envía cadena vacía '' para vaciarlas. Para 'apps a abrir' usa el destino de list_known_apps; para 'cerrar/silenciar' el nombre de proceso.")]
    public string UpsertFocusEnvironment(
        [Description("Id estable del entorno, p. ej. 'deep'")] string id,
        [Description("Nombre visible, p. ej. 'Estudio profundo'")] string name,
        [Description("Activar No molestar (null = mantener)")] bool? doNotDisturb = null,
        [Description("Ocultar distintivos de la barra de tareas (null = mantener)")] bool? hideTaskbarBadges = null,
        [Description("Mostrar vista previa del día al iniciar (null = mantener)")] bool? showDayPreview = null,
        [Description("Abrir los enlaces en una ventana nueva del navegador (null = mantener)")] bool? openLinksInBrowser = null,
        [Description("Crear un escritorio virtual nuevo al concentrarte (null = mantener)")] bool? newVirtualDesktop = null,
        [Description("Id del ritmo Pomodoro del entorno ('classic', 'deepwork' o uno propio). null = mantener")] string? pomodoroPreset = null,
        [Description("Webs a bloquear, csv (null = mantener, '' = vaciar)")] string? blockedWebsites = null,
        [Description("Apps a cerrar, csv de nombres de proceso (null = mantener, '' = vaciar)")] string? appsToClose = null,
        [Description("Apps a silenciar, csv (null = mantener, '' = vaciar)")] string? appsToMute = null,
        [Description("Apps a ABRIR al concentrarte, csv (null = mantener, '' = vaciar)")] string? appsToOpen = null,
        [Description("Nombre de la música a lanzar (deja music y target vacíos para mantener la actual)")] string? musicName = null,
        [Description("Ejecutable o URI de la música, p. ej. 'spotify:'")] string? musicTarget = null)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            return Err("El entorno necesita id y nombre.");

        // Partimos del entorno existente (si lo hay) para no perder lo que no se toca.
        var existing = _config.GetSettings().FocusEnvironments.FirstOrDefault(e => e.Id == id);
        var baseEnv = existing ?? new FocusEnvironment { Id = id, Name = name };

        var env = baseEnv with
        {
            Name = name,
            EnableDoNotDisturb = doNotDisturb ?? baseEnv.EnableDoNotDisturb,
            HideTaskbarBadges = hideTaskbarBadges ?? baseEnv.HideTaskbarBadges,
            ShowDayPreview = showDayPreview ?? baseEnv.ShowDayPreview,
            OpenLinksInBrowser = openLinksInBrowser ?? baseEnv.OpenLinksInBrowser,
            NewVirtualDesktop = newVirtualDesktop ?? baseEnv.NewVirtualDesktop,
            PomodoroPreset = pomodoroPreset ?? baseEnv.PomodoroPreset,
            BlockedWebsites = blockedWebsites is null ? baseEnv.BlockedWebsites : Split(blockedWebsites),
            AppsToClose = appsToClose is null ? baseEnv.AppsToClose : Split(appsToClose),
            AppsToMute = appsToMute is null ? baseEnv.AppsToMute : Split(appsToMute),
            AppsToOpen = appsToOpen is null ? baseEnv.AppsToOpen : Split(appsToOpen),
            // Enlaces, tareas y perfiles se conservan SIEMPRE (herramientas dedicadas).
            Music = (string.IsNullOrWhiteSpace(musicName) || string.IsNullOrWhiteSpace(musicTarget))
                ? baseEnv.Music
                : new MusicLauncher { Name = musicName!, Target = musicTarget!, AutoPlay = true }
        };
        return Report(_config.UpsertEnvironment(env));
    }

    [McpServerTool(Name = "remove_environment")]
    [Description("Elimina un entorno por su id. Si era el por defecto o estaba mapeado a algún tipo, esas referencias se limpian solas.")]
    public string RemoveEnvironment([Description("Id del entorno")] string environmentId)
        => Report(_config.RemoveEnvironment(environmentId));

    [McpServerTool(Name = "set_default_environment")]
    [Description("Fija el entorno de concentración por defecto, por su id (debe existir). Pasa vacío para quitar el por defecto (modo automático).")]
    public string SetDefaultEnvironment([Description("Id del entorno (vacío = ninguno)")] string? environmentId = null)
        => Report(_config.SetDefaultEnvironment(environmentId));

    [McpServerTool(Name = "add_environment_link")]
    [Description("Añade un enlace (título + URL) a los accesos rápidos de un entorno (por id).")]
    public string AddEnvironmentLink(
        [Description("Id del entorno")] string environmentId,
        [Description("Título del enlace")] string title,
        [Description("URL")] string url)
        => Report(_config.AddEnvironmentLink(environmentId, title, url));

    [McpServerTool(Name = "remove_environment_link")]
    [Description("Elimina el enlace en el índice dado de un entorno (posición en su array 'links', empezando en 0).")]
    public string RemoveEnvironmentLink(
        [Description("Id del entorno")] string environmentId,
        [Description("Índice del enlace (0 = primero)")] int index)
        => Report(_config.RemoveEnvironmentLink(environmentId, index));

    [McpServerTool(Name = "add_environment_task")]
    [Description("Añade una tarea (to-do) a un entorno. Devuelve su id.")]
    public string AddEnvironmentTask(
        [Description("Id del entorno")] string environmentId,
        [Description("Texto de la tarea")] string text)
        => Report(_config.AddEnvironmentTask(environmentId, text));

    [McpServerTool(Name = "toggle_environment_task")]
    [Description("Marca/desmarca como hecha una tarea de un entorno (por id de entorno + id de tarea).")]
    public string ToggleEnvironmentTask(
        [Description("Id del entorno")] string environmentId,
        [Description("Id de la tarea")] string taskId)
        => Report(_config.ToggleEnvironmentTask(environmentId, taskId));

    [McpServerTool(Name = "remove_environment_task")]
    [Description("Elimina una tarea de un entorno (por id de entorno + id de tarea).")]
    public string RemoveEnvironmentTask(
        [Description("Id del entorno")] string environmentId,
        [Description("Id de la tarea")] string taskId)
        => Report(_config.RemoveEnvironmentTask(environmentId, taskId));

    [McpServerTool(Name = "move_environment_task")]
    [Description("Reordena una tarea de un entorno una posición arriba (up=true) o abajo (up=false).")]
    public string MoveEnvironmentTask(
        [Description("Id del entorno")] string environmentId,
        [Description("Id de la tarea")] string taskId,
        [Description("true = subir, false = bajar")] bool up)
        => Report(_config.MoveEnvironmentTask(environmentId, taskId, up));

    // ==================== PERFILES POR TIPO DE SESIÓN (en un entorno) ====================

    [McpServerTool(Name = "set_session_profile")]
    [Description("Define qué enlaces (URLs) y apps (procesos) se abren para un TIPO de sesión (por título) dentro de un entorno. Las URLs/apps deben existir entre los enlaces/apps-a-abrir del entorno. Reemplaza el perfil previo de ese título. Sin perfil, una sesión abre TODO lo del entorno.")]
    public string SetSessionProfile(
        [Description("Id del entorno")] string environmentId,
        [Description("Título de la sesión (grupo)")] string sessionTitle,
        [Description("URLs de enlaces a abrir, csv")] string? enabledLinks = null,
        [Description("Apps a abrir, csv")] string? enabledApps = null)
        => Report(_config.SetSessionProfile(environmentId, sessionTitle, Split(enabledLinks), Split(enabledApps)));

    [McpServerTool(Name = "clear_session_profile")]
    [Description("Olvida el perfil de un tipo de sesión en un entorno (vuelve a abrir TODO para ese título).")]
    public string ClearSessionProfile(
        [Description("Id del entorno")] string environmentId,
        [Description("Título de la sesión")] string sessionTitle)
        => Report(_config.ClearSessionProfile(environmentId, sessionTitle));

    // ==================== MAPEO TIPO → ENTORNO ====================

    [McpServerTool(Name = "map_environment_to_kind")]
    [Description("Asocia un tipo de bloque a un entorno, para que ese tipo lo use automáticamente al iniciar focus. El entorno debe existir.")]
    public string MapEnvironmentToKind(
        [Description("Tipo: " + KindList)] string kind,
        [Description("Id del entorno")] string environmentId)
    {
        return Report(_config.MapEnvironmentToKind(kind, environmentId));
    }

    [McpServerTool(Name = "clear_environment_kind")]
    [Description("Quita la asociación tipo→entorno: ese tipo vuelve a usar el entorno por defecto.")]
    public string ClearEnvironmentKind([Description("Tipo: " + KindList)] string kind)
    {
        return Report(_config.ClearEnvironmentKind(kind));
    }

    // ==================== CATEGORÍAS DE BLOQUE (#83) ====================

    [McpServerTool(Name = "list_categories")]
    [Description("Lista las categorías de bloque del usuario: id, nombre, si dispara concentración, si es de sistema y su color.")]
    public string ListCategories()
    {
        var cats = _config.GetSettings().Categories.OrderBy(c => c.Order).ToList();
        if (cats.Count == 0) return "No hay categorías.";
        return string.Join("\n", cats.Select(c =>
            $"- {c.Id}: \"{c.Name}\"{(c.IsFocus ? " [concentración]" : "")}{(c.IsSystem ? " [sistema]" : "")} {c.ColorHex}"));
    }

    [McpServerTool(Name = "add_category")]
    [Description("Crea una categoría de bloque. Devuelve su id. color en #RRGGBB; isFocus=true si dispara concentración.")]
    public string AddCategory(
        [Description("Nombre visible")] string name,
        [Description("Color de fondo #RRGGBB")] string colorHex,
        [Description("true si dispara el modo concentración")] bool isFocus = false)
        => Report(_config.AddCategory(name, colorHex, isFocus));

    [McpServerTool(Name = "update_category")]
    [Description("Actualiza nombre, color y focus de una categoría (por id; el id no cambia).")]
    public string UpdateCategory(
        [Description("Id de la categoría")] string id,
        [Description("Nombre visible")] string name,
        [Description("Color de fondo #RRGGBB")] string colorHex,
        [Description("true si dispara el modo concentración")] bool isFocus = false)
        => Report(_config.UpdateCategory(id, name, colorHex, isFocus));

    [McpServerTool(Name = "remove_category")]
    [Description("Elimina una categoría (no las de sistema «Otro»/«Por definir»). Sus bloques pasan a «Otro».")]
    public string RemoveCategory([Description("Id de la categoría")] string id)
        => Report(_config.RemoveCategory(id));

    [McpServerTool(Name = "reorder_category")]
    [Description("Reordena una categoría una posición arriba (up=true) o abajo (up=false).")]
    public string ReorderCategory(
        [Description("Id de la categoría")] string id,
        [Description("true = subir, false = bajar")] bool up)
        => Report(_config.ReorderCategory(id, up));

    // ==================== MÚSICA (Navidrome global) ====================

    [McpServerTool(Name = "set_navidrome_connection")]
    [Description("Guarda la conexión GLOBAL a Navidrome (URL del servidor + usuario). La CONTRASEÑA NO se configura por aquí: el usuario debe introducirla en la app (se guarda en el almacén seguro del sistema).")]
    public string SetNavidromeConnection(
        [Description("URL del servidor Navidrome")] string serverUrl,
        [Description("Usuario")] string user)
        => Report(_config.SetNavidromeConnection(serverUrl, user));

    [McpServerTool(Name = "clear_navidrome_connection")]
    [Description("Elimina la conexión global a Navidrome (servidor + usuario).")]
    public string ClearNavidromeConnection()
        => Report(_config.ClearNavidromeConnection());

    // ==================== NOTIFICACIONES AL MÓVIL (ntfy) ====================

    [McpServerTool(Name = "set_ntfy")]
    [Description("Configura las notificaciones push al móvil vía ntfy (#122). enabled=true las activa (requiere topic); enabled=false las desactiva. serverUrl vacío = https://ntfy.sh. El topic es un secreto compartido: el usuario instala la app ntfy (Android/iOS) y se suscribe al MISMO topic para recibir los avisos del horario en el teléfono.")]
    public string SetNtfy(
        [Description("Activar (true) o desactivar (false) el push al móvil")] bool enabled,
        [Description("Topic de ntfy (obligatorio si enabled=true)")] string? topic = null,
        [Description("URL del servidor ntfy (vacío = https://ntfy.sh)")] string? serverUrl = null)
        => Report(_config.SetNtfy(enabled, serverUrl, topic));

    // ==================== CALENDARIOS (ICS) ====================

    [McpServerTool(Name = "add_calendar_feed")]
    [Description("Suscribe un calendario externo por enlace ICS (http/https/webcal), solo lectura. Devuelve su id.")]
    public string AddCalendarFeed(
        [Description("Nombre visible del calendario")] string name,
        [Description("Enlace ICS (http/https/webcal)")] string url)
        => Report(_config.AddCalendarFeed(name, url));

    [McpServerTool(Name = "remove_calendar_feed")]
    [Description("Elimina una suscripción de calendario por su id.")]
    public string RemoveCalendarFeed([Description("Id del calendario")] string id)
        => Report(_config.RemoveCalendarFeed(id));

    // ==================== PRIORIDAD DE SOLAPAMIENTO ====================

    [McpServerTool(Name = "set_overlap_priority")]
    [Description("Recuerda qué lado prioriza el usuario cuando un evento del calendario se solapa con una sesión. eventKey es la clave del evento (campo 'eventKey' en overlapPriorities de get_config). preferCalendar=true prioriza el evento; false prioriza la sesión.")]
    public string SetOverlapPriority(
        [Description("Clave estable del evento en conflicto")] string eventKey,
        [Description("true = priorizar el evento del calendario; false = priorizar la sesión")] bool preferCalendar)
        => Report(_config.SetOverlapPriority(eventKey, preferCalendar));

    [McpServerTool(Name = "clear_overlap_priority")]
    [Description("Olvida la decisión de prioridad de un evento solapado (por su eventKey).")]
    public string ClearOverlapPriority([Description("Clave del evento")] string eventKey)
        => Report(_config.ClearOverlapPriority(eventKey));

    // ==================== IMPORTAR / EXPORTAR ====================

    [McpServerTool(Name = "import_config")]
    [Description("ATENCIÓN: reemplaza TODA la configuración por la del JSON dado (como restaurar un respaldo). Úsalo solo con un JSON con el mismo formato que devuelve get_config y tras confirmarlo con el usuario. Si el JSON no es válido, no toca nada.")]
    public string ImportConfig([Description("JSON de configuración completo (formato de get_config)")] string json)
        => Report(_config.ImportJson(json));

    // ---------- helpers ----------
    private static bool TryDate(string s, out DateOnly d) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

    private static bool TryOptionalDate(string? s, out DateOnly? d)
    {
        d = null;
        if (string.IsNullOrWhiteSpace(s)) return true;
        if (!TryDate(s, out var parsed)) return false;
        d = parsed;
        return true;
    }

    private static bool TryTime(string s, out TimeOnly t) =>
        TimeOnly.TryParseExact(s, "HH\\:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t);

    private static string ParseKind(string kind) =>
        string.IsNullOrWhiteSpace(kind) ? Ritmo.Core.Model.CategoryIds.Other : kind.Trim();

    private static List<PreAlert> ParseAlerts(string? csv)
    {
        var alerts = new List<PreAlert>();
        if (string.IsNullOrWhiteSpace(csv)) return alerts;
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(part, out var m)) alerts.Add(new PreAlert(m));
        return alerts;
    }

    private static List<string> Split(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? []
        : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string Report(CommandResult r) =>
        r.Success ? $"OK: {r.Message}" : $"ERROR: {string.Join(" | ", r.Errors.DefaultIfEmpty(r.Message))}";

    private static string Err(string msg) => $"ERROR: {msg}";
}
