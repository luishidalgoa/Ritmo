using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;
using Ritmo.Core.Pomodoro;

namespace Ritmo.Core.Commands;

/// <summary>
/// Fachada de configuración: aplica cambios validados sobre el estado persistido.
/// La consumen por igual la UI y la API para IA, de modo que hay UN solo punto
/// de verdad y validación. Cada comando carga el estado, valida, aplica y guarda.
/// </summary>
public sealed class ConfigurationService
{
    private readonly ISettingsStore _store;

    public ConfigurationService(ISettingsStore store) => _store = store;

    /// <summary>Lee el estado actual (sin modificar nada).</summary>
    public AppSettings GetSettings() => _store.Load();

    /// <summary>Serializa toda la configuración a JSON (para exportar / respaldar). #56</summary>
    public string ExportJson() => SettingsJson.Serialize(_store.Load());

    /// <summary>
    /// Reemplaza TODA la configuración por la de un JSON exportado. Valida que el
    /// JSON sea parseable antes de guardar; si no, no toca nada. #56
    /// </summary>
    public CommandResult ImportJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return CommandResult.Fail("El archivo está vacío.");

        AppSettings imported;
        try
        {
            imported = SettingsJson.Deserialize(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return CommandResult.Fail("El archivo no es una configuración válida de Ritmo.");
        }

        _store.Save(imported);
        return CommandResult.Ok("Configuración importada.");
    }

    /// <summary>Resumen del estado para responder a la IA o pintar la UI.</summary>
    public StatusReport GetStatus()
    {
        var s = _store.Load();
        return new StatusReport
        {
            PhaseCount = s.Plan.Phases.Count,
            PhaseNames = s.Plan.OrderedPhases.Select(p => p.Name).ToList(),
            EnvironmentCount = s.FocusEnvironments.Count,
            EnvironmentNames = s.FocusEnvironments.Select(e => e.Name).ToList(),
            DefaultEnvironmentId = s.DefaultFocusEnvironmentId,
            NoteCount = s.Notes.Count
        };
    }

    /// <summary>Añade una fase nueva al plan, validando nombre y vigencia.</summary>
    public CommandResult AddPhase(string name, DateOnly validFrom, DateOnly? validTo)
    {
        if (string.IsNullOrWhiteSpace(name))
            return CommandResult.Fail("El nombre de la fase no puede estar vacío.");
        if (validTo is { } end && end < validFrom)
            return CommandResult.Fail("La fecha de fin no puede ser anterior a la de inicio.");

        var s = _store.Load();
        if (s.Plan.Phases.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return CommandResult.Fail($"Ya existe una fase llamada «{name}».");

        var phase = new SchedulePhase { Name = name.Trim(), ValidFrom = validFrom, ValidTo = validTo };
        var updated = s with { Plan = new SchedulePlan { Phases = [.. s.Plan.Phases, phase] } };
        _store.Save(updated);
        return CommandResult.Ok($"Fase «{name}» añadida.");
    }

    /// <summary>Renombra y/o cambia la vigencia de una fase existente (#46).</summary>
    public CommandResult UpdatePhase(string name, string newName, DateOnly validFrom, DateOnly? validTo)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return CommandResult.Fail("El nombre de la fase no puede estar vacío.");
        if (validTo is { } end && end < validFrom)
            return CommandResult.Fail("La fecha de fin no puede ser anterior a la de inicio.");

        var s = _store.Load();
        var phase = s.Plan.Phases.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (phase is null) return CommandResult.Fail($"No existe la fase «{name}».");
        if (!newName.Trim().Equals(name, StringComparison.OrdinalIgnoreCase) &&
            s.Plan.Phases.Any(p => p.Name.Equals(newName.Trim(), StringComparison.OrdinalIgnoreCase)))
            return CommandResult.Fail($"Ya existe una fase llamada «{newName.Trim()}».");

        var updated = phase with { Name = newName.Trim(), ValidFrom = validFrom, ValidTo = validTo };
        var newPhases = s.Plan.Phases.Select(p => ReferenceEquals(p, phase) ? updated : p).ToList();
        _store.Save(s with { Plan = new SchedulePlan { Phases = newPhases } });
        return CommandResult.Ok("Fase actualizada.");
    }

    /// <summary>Elimina una fase del plan. Debe quedar al menos una (#46).</summary>
    public CommandResult RemovePhase(string name)
    {
        var s = _store.Load();
        if (s.Plan.Phases.Count <= 1) return CommandResult.Fail("Debe quedar al menos una fase.");
        var phase = s.Plan.Phases.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (phase is null) return CommandResult.Fail($"No existe la fase «{name}».");
        var newPhases = s.Plan.Phases.Where(p => !ReferenceEquals(p, phase)).ToList();
        _store.Save(s with { Plan = new SchedulePlan { Phases = newPhases } });
        return CommandResult.Ok($"Fase «{name}» eliminada.");
    }

    /// <summary>Añade una sesión a una fase existente (por nombre).</summary>
    public CommandResult AddSession(string phaseName, StudySession session)
    {
        if (session.Duration <= TimeSpan.Zero)
            return CommandResult.Fail("La duración debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(session.Title))
            return CommandResult.Fail("La sesión necesita un título.");

        var s = _store.Load();
        var phase = s.Plan.Phases.FirstOrDefault(p => p.Name.Equals(phaseName, StringComparison.OrdinalIgnoreCase));
        if (phase is null)
            return CommandResult.Fail($"No existe la fase «{phaseName}».");

        var newPhase = phase with
        {
            Schedule = new WeeklySchedule { Sessions = [.. phase.Schedule.Sessions, session] }
        };
        var newPhases = s.Plan.Phases.Select(p => ReferenceEquals(p, phase) ? newPhase : p).ToList();
        _store.Save(s with { Plan = new SchedulePlan { Phases = newPhases } });
        return CommandResult.Ok($"Sesión «{session.Title}» añadida a «{phaseName}».");
    }

    /// <summary>Reemplaza la sesión en el índice dado de una fase.</summary>
    public CommandResult UpdateSession(string phaseName, int index, StudySession session)
    {
        if (session.Duration <= TimeSpan.Zero)
            return CommandResult.Fail("La duración debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(session.Title))
            return CommandResult.Fail("La sesión necesita un título.");

        var s = _store.Load();
        var phase = s.Plan.Phases.FirstOrDefault(p => p.Name.Equals(phaseName, StringComparison.OrdinalIgnoreCase));
        if (phase is null)
            return CommandResult.Fail($"No existe la fase «{phaseName}».");
        if (index < 0 || index >= phase.Schedule.Sessions.Count)
            return CommandResult.Fail("Índice de sesión fuera de rango.");

        var list = phase.Schedule.Sessions.ToList();
        list[index] = session;
        var newPhase = phase with { Schedule = new WeeklySchedule { Sessions = list } };
        var newPhases = s.Plan.Phases.Select(p => ReferenceEquals(p, phase) ? newPhase : p).ToList();
        _store.Save(s with { Plan = new SchedulePlan { Phases = newPhases } });
        return CommandResult.Ok($"Sesión actualizada en «{phaseName}».");
    }

    /// <summary>Elimina la sesión en el índice dado de una fase.</summary>
    public CommandResult RemoveSession(string phaseName, int index)
    {
        var s = _store.Load();
        var phase = s.Plan.Phases.FirstOrDefault(p => p.Name.Equals(phaseName, StringComparison.OrdinalIgnoreCase));
        if (phase is null)
            return CommandResult.Fail($"No existe la fase «{phaseName}».");
        if (index < 0 || index >= phase.Schedule.Sessions.Count)
            return CommandResult.Fail("Índice de sesión fuera de rango.");

        var list = phase.Schedule.Sessions.ToList();
        list.RemoveAt(index);
        var newPhase = phase with { Schedule = new WeeklySchedule { Sessions = list } };
        var newPhases = s.Plan.Phases.Select(p => ReferenceEquals(p, phase) ? newPhase : p).ToList();
        _store.Save(s with { Plan = new SchedulePlan { Phases = newPhases } });
        return CommandResult.Ok($"Sesión eliminada de «{phaseName}».");
    }

    /// <summary>
    /// Reemplaza TODAS las sesiones de una fase por la lista dada. Útil para editar
    /// o borrar un grupo de sesiones fusionadas (#86) de una vez. Valida cada sesión.
    /// </summary>
    public CommandResult ReplaceSessions(string phaseName, IReadOnlyList<StudySession> sessions)
    {
        if (sessions.Any(s => s.Duration <= TimeSpan.Zero))
            return CommandResult.Fail("Alguna sesión tiene duración cero o negativa.");
        if (sessions.Any(s => string.IsNullOrWhiteSpace(s.Title)))
            return CommandResult.Fail("Alguna sesión no tiene título.");

        var s = _store.Load();
        var phase = s.Plan.Phases.FirstOrDefault(p => p.Name.Equals(phaseName, StringComparison.OrdinalIgnoreCase));
        if (phase is null) return CommandResult.Fail($"No existe la fase «{phaseName}».");

        var newPhase = phase with { Schedule = new WeeklySchedule { Sessions = sessions.ToList() } };
        var newPhases = s.Plan.Phases.Select(p => ReferenceEquals(p, phase) ? newPhase : p).ToList();
        _store.Save(s with { Plan = new SchedulePlan { Phases = newPhases } });
        return CommandResult.Ok("Sesiones actualizadas.");
    }

    // ---------- Sesiones provisionales (con fecha, #103) ----------

    /// <summary>Añade una sesión provisional (extraordinaria) en una fecha concreta. Devuelve su Id.</summary>
    public CommandResult AddOneOffSession(DateOnly date, string title, TimeOnly start, TimeSpan duration,
        string categoryId, IReadOnlyList<PreAlert> preAlerts, bool isTentative)
    {
        if (duration <= TimeSpan.Zero) return CommandResult.Fail("La duración debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(title)) return CommandResult.Fail("La sesión necesita un título.");
        var s = _store.Load();
        var one = new OneOffSession
        {
            Id = $"oneoff-{Guid.NewGuid():N}"[..14],
            Date = date, Title = title.Trim(), Start = start, Duration = duration,
            CategoryId = string.IsNullOrWhiteSpace(categoryId) ? CategoryIds.Other : categoryId,
            PreAlerts = preAlerts.ToList(), IsTentative = isTentative
        };
        _store.Save(s with { OneOffSessions = [.. s.OneOffSessions, one] });
        return CommandResult.Ok(one.Id);
    }

    /// <summary>Elimina una sesión provisional por Id.</summary>
    public CommandResult RemoveOneOffSession(string id)
    {
        var s = _store.Load();
        if (s.OneOffSessions.All(o => o.Id != id)) return CommandResult.Fail("No existe la sesión provisional.");
        _store.Save(s with { OneOffSessions = s.OneOffSessions.Where(o => o.Id != id).ToList() });
        return CommandResult.Ok("Sesión provisional eliminada.");
    }

    /// <summary>Actualiza la configuración Pomodoro (duraciones en minutos).</summary>
    public CommandResult SetPomodoro(int focusMin, int shortBreakMin, int longBreakMin, int focusesPerLong)
    {
        if (focusMin <= 0) return CommandResult.Fail("La concentración debe durar más de 0 minutos.");
        if (focusesPerLong < 1) return CommandResult.Fail("Debe haber al menos 1 foco por descanso largo.");
        try
        {
            var cfg = new Pomodoro.PomodoroConfig(
                TimeSpan.FromMinutes(focusMin), TimeSpan.FromMinutes(shortBreakMin),
                TimeSpan.FromMinutes(longBreakMin), focusesPerLong);
            _store.Save(_store.Load() with { Pomodoro = cfg });
            return CommandResult.Ok("Pomodoro actualizado.");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return CommandResult.Fail(ex.Message);
        }
    }

    // ---------- Ritmos Pomodoro personalizados (#96) ----------

    /// <summary>Crea un ritmo Pomodoro propio. Devuelve su Id en el mensaje.</summary>
    public CommandResult AddRhythm(string name, int focusMin, int shortMin, int longMin, int focusesPerLong)
    {
        var error = ValidateRhythm(name, focusMin, focusesPerLong);
        if (error is not null) return CommandResult.Fail(error);

        var s = _store.Load();
        var rhythm = new PomodoroRhythm
        {
            Id = $"rhythm-{Guid.NewGuid():N}"[..14],
            Name = name.Trim(),
            FocusMinutes = focusMin,
            ShortBreakMinutes = Math.Max(0, shortMin),
            LongBreakMinutes = Math.Max(0, longMin),
            FocusesPerLongBreak = focusesPerLong
        };
        _store.Save(s with { Rhythms = [.. s.Rhythms, rhythm] });
        return CommandResult.Ok(rhythm.Id);
    }

    /// <summary>Edita un ritmo propio existente (por Id).</summary>
    public CommandResult UpdateRhythm(string id, string name, int focusMin, int shortMin, int longMin, int focusesPerLong)
    {
        var error = ValidateRhythm(name, focusMin, focusesPerLong);
        if (error is not null) return CommandResult.Fail(error);

        var s = _store.Load();
        if (s.Rhythms.All(r => r.Id != id)) return CommandResult.Fail($"No existe el ritmo «{id}».");
        var updated = s.Rhythms.Select(r => r.Id == id
            ? r with
            {
                Name = name.Trim(), FocusMinutes = focusMin,
                ShortBreakMinutes = Math.Max(0, shortMin), LongBreakMinutes = Math.Max(0, longMin),
                FocusesPerLongBreak = focusesPerLong
            }
            : r).ToList();
        _store.Save(s with { Rhythms = updated });
        return CommandResult.Ok("Ritmo actualizado.");
    }

    /// <summary>Elimina un ritmo propio por Id.</summary>
    public CommandResult RemoveRhythm(string id)
    {
        var s = _store.Load();
        if (s.Rhythms.All(r => r.Id != id)) return CommandResult.Fail($"No existe el ritmo «{id}».");
        _store.Save(s with { Rhythms = s.Rhythms.Where(r => r.Id != id).ToList() });
        return CommandResult.Ok("Ritmo eliminado.");
    }

    private static string? ValidateRhythm(string name, int focusMin, int focusesPerLong)
    {
        if (string.IsNullOrWhiteSpace(name)) return "El ritmo necesita un nombre.";
        if (focusMin <= 0) return "La concentración debe durar más de 0 minutos.";
        if (focusesPerLong < 1) return "Debe haber al menos 1 foco por descanso largo.";
        return null;
    }

    /// <summary>Actualiza el rango horario visible de la rejilla del horario.</summary>
    public CommandResult SetViewHours(TimeOnly dayStart, TimeOnly dayEnd)
    {
        if (dayEnd <= dayStart) return CommandResult.Fail("La hora de fin debe ser posterior a la de inicio.");
        var s = _store.Load();
        _store.Save(s with { ViewConfig = s.ViewConfig with { DayStart = dayStart, DayEnd = dayEnd } });
        return CommandResult.Ok("Rango horario actualizado.");
    }

    /// <summary>
    /// Fija (hex "#RRGGBB") o quita (hex vacío → vuelve al color por defecto) el color
    /// de fondo de un tipo de bloque en la rejilla del horario. #45
    /// </summary>
    public CommandResult SetKindColor(string categoryId, string? hex)
    {
        var s = _store.Load();
        var cat = s.Categories.FirstOrDefault(c => c.Id == categoryId);
        if (cat is null) return CommandResult.Fail($"No existe la categoría «{categoryId}».");

        string newColor;
        if (string.IsNullOrWhiteSpace(hex))
            newColor = Ritmo.Core.Model.LegacyCategories.ById.TryGetValue(categoryId, out var legacy)
                ? legacy.ColorHex : "#EDEDED";   // sin hex → color base (legacy si lo hubiera, o gris)
        else
        {
            var norm = NormalizeHexColor(hex);
            if (norm is null) return CommandResult.Fail("Color inválido. Usa el formato #RRGGBB.");
            newColor = norm;
        }
        var updated = s.Categories.Select(c => c.Id == categoryId ? c with { ColorHex = newColor } : c).ToList();
        _store.Save(s with { Categories = updated });
        return CommandResult.Ok(string.IsNullOrWhiteSpace(hex)
            ? $"Color de «{cat.Name}» restablecido."
            : $"Color de «{cat.Name}» actualizado.");
    }

    /// <summary>Normaliza un color a "#RRGGBB" en mayúsculas; null si no es válido.</summary>
    private static string? NormalizeHexColor(string hex)
    {
        var h = hex.Trim().TrimStart('#');
        if (h.Length != 6) return null;
        foreach (var c in h) if (!Uri.IsHexDigit(c)) return null;
        return "#" + h.ToUpperInvariant();
    }

    /// <summary>Marca como vistas las «Novedades» hasta la versión dada (carrusel de updates).</summary>
    public CommandResult SetLastSeenVersion(string version)
    {
        var s = _store.Load();
        _store.Save(s with { LastSeenVersion = string.IsNullOrWhiteSpace(version) ? null : version.Trim() });
        return CommandResult.Ok("Novedades marcadas como vistas.");
    }

    /// <summary>
    /// Activa/desactiva la vista previa del día al iniciar concentración (#47): si
    /// está activa, al arrancar el foco se muestra un resumen de los bloques de hoy.
    /// </summary>
    public CommandResult SetShowDayPreviewOnFocusStart(bool show)
    {
        var s = _store.Load();
        _store.Save(s with { ViewConfig = s.ViewConfig with { ShowDayPreviewOnFocusStart = show } });
        return CommandResult.Ok(show ? "Vista previa del día activada." : "Vista previa del día desactivada.");
    }

    /// <summary>
    /// Fija la granularidad de la rejilla de fondo del horario (60, 30 o 15 min).
    /// Solo afecta a las líneas-guía; los bloques se siguen posicionando por su
    /// minuto real. #61
    /// </summary>
    public CommandResult SetGranularity(int minutes)
    {
        if (minutes is not (60 or 30 or 15))
            return CommandResult.Fail("La granularidad debe ser 60, 30 o 15 minutos.");
        var s = _store.Load();
        _store.Save(s with { ViewConfig = s.ViewConfig with { GranularityMinutes = minutes } });
        return CommandResult.Ok($"Granularidad fijada en {minutes} min.");
    }

    // ---------- Notas y enlaces-atajo (#55) ----------

    /// <summary>
    /// Añade una nota fijada (markdown). Devuelve su Id en el mensaje. Si se pasa
    /// <paramref name="sessionTitle"/>, la nota es un "post-it" de esa sesión (#73).
    /// </summary>
    public CommandResult AddNote(string title, string content, string? accentColor = null, string? sessionTitle = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return CommandResult.Fail("La nota necesita un título.");
        var s = _store.Load();
        var order = s.Notes.Count == 0 ? 0 : s.Notes.Max(n => n.Order) + 1;
        var note = new StudyNote
        {
            Id = $"note-{Guid.NewGuid():N}"[..12],
            Title = title.Trim(),
            Content = content ?? "",
            AccentColor = accentColor,
            Order = order,
            SessionTitle = string.IsNullOrWhiteSpace(sessionTitle) ? null : sessionTitle.Trim()
        };
        _store.Save(s with { Notes = [.. s.Notes, note] });
        return CommandResult.Ok(note.Id);
    }

    /// <summary>Edita el título/contenido de una nota existente.</summary>
    public CommandResult UpdateNote(string id, string title, string content, string? accentColor = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return CommandResult.Fail("La nota necesita un título.");
        var s = _store.Load();
        var note = s.Notes.FirstOrDefault(n => n.Id == id);
        if (note is null) return CommandResult.Fail($"No existe la nota «{id}».");
        var updated = s.Notes
            .Select(n => n.Id == id ? n with { Title = title.Trim(), Content = content ?? "", AccentColor = accentColor } : n)
            .ToList();
        _store.Save(s with { Notes = updated });
        return CommandResult.Ok("Nota actualizada.");
    }

    /// <summary>Elimina una nota por Id.</summary>
    public CommandResult RemoveNote(string id)
    {
        var s = _store.Load();
        if (s.Notes.All(n => n.Id != id)) return CommandResult.Fail($"No existe la nota «{id}».");
        _store.Save(s with { Notes = s.Notes.Where(n => n.Id != id).ToList() });
        return CommandResult.Ok("Nota eliminada.");
    }

    /// <summary>Añade un enlace-atajo (título + URL).</summary>
    public CommandResult AddShortcut(string title, string url)
    {
        if (string.IsNullOrWhiteSpace(title)) return CommandResult.Fail("El enlace necesita un título.");
        if (string.IsNullOrWhiteSpace(url)) return CommandResult.Fail("El enlace necesita una URL.");
        var s = _store.Load();
        var list = s.ViewConfig.Shortcuts.Append(new ShortcutLink { Title = title.Trim(), Url = url.Trim() }).ToList();
        _store.Save(s with { ViewConfig = s.ViewConfig with { Shortcuts = list } });
        return CommandResult.Ok($"Enlace «{title}» añadido.");
    }

    /// <summary>Elimina el enlace-atajo en el índice dado.</summary>
    public CommandResult RemoveShortcut(int index)
    {
        var s = _store.Load();
        if (index < 0 || index >= s.ViewConfig.Shortcuts.Count)
            return CommandResult.Fail("Índice de enlace fuera de rango.");
        var list = s.ViewConfig.Shortcuts.ToList();
        list.RemoveAt(index);
        _store.Save(s with { ViewConfig = s.ViewConfig with { Shortcuts = list } });
        return CommandResult.Ok("Enlace eliminado.");
    }

    /// <summary>Crea o reemplaza un entorno de concentración (por Id).</summary>
    public CommandResult UpsertEnvironment(FocusEnvironment env)
    {
        if (string.IsNullOrWhiteSpace(env.Id) || string.IsNullOrWhiteSpace(env.Name))
            return CommandResult.Fail("El entorno necesita Id y nombre.");

        var s = _store.Load();
        var others = s.FocusEnvironments.Where(e => e.Id != env.Id).ToList();
        others.Add(env);
        _store.Save(s with { FocusEnvironments = others });
        return CommandResult.Ok($"Entorno «{env.Name}» guardado.");
    }

    /// <summary>
    /// Elimina un entorno. Si era el por defecto, lo deja sin por defecto; y quita
    /// cualquier mapeo tipo→entorno que apuntara a él (para no dejar referencias rotas).
    /// </summary>
    public CommandResult RemoveEnvironment(string environmentId)
    {
        var s = _store.Load();
        if (s.FocusEnvironments.All(e => e.Id != environmentId))
            return CommandResult.Fail($"No existe el entorno con id «{environmentId}».");

        var remaining = s.FocusEnvironments.Where(e => e.Id != environmentId).ToList();
        var newDefault = s.DefaultFocusEnvironmentId == environmentId ? null : s.DefaultFocusEnvironmentId;
        var newMap = s.EnvironmentByKind
            .Where(kv => kv.Value != environmentId)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        _store.Save(s with
        {
            FocusEnvironments = remaining,
            DefaultFocusEnvironmentId = newDefault,
            EnvironmentByKind = newMap
        });
        return CommandResult.Ok("Entorno eliminado.");
    }

    /// <summary>Añade un enlace al entorno de trabajo indicado. #74</summary>
    public CommandResult AddEnvironmentLink(string environmentId, string title, string url)
    {
        if (string.IsNullOrWhiteSpace(title)) return CommandResult.Fail("El enlace necesita un título.");
        if (string.IsNullOrWhiteSpace(url)) return CommandResult.Fail("El enlace necesita una URL.");
        var s = _store.Load();
        var env = s.FocusEnvironments.FirstOrDefault(e => e.Id == environmentId);
        if (env is null) return CommandResult.Fail($"No existe el entorno «{environmentId}».");

        var updated = env with { Links = [.. env.Links, new ShortcutLink { Title = title.Trim(), Url = url.Trim() }] };
        _store.Save(s with { FocusEnvironments = s.FocusEnvironments.Select(e => e.Id == environmentId ? updated : e).ToList() });
        return CommandResult.Ok($"Enlace «{title}» añadido a «{env.Name}».");
    }

    /// <summary>Elimina el enlace en el índice dado del entorno indicado. #74</summary>
    public CommandResult RemoveEnvironmentLink(string environmentId, int index)
    {
        var s = _store.Load();
        var env = s.FocusEnvironments.FirstOrDefault(e => e.Id == environmentId);
        if (env is null) return CommandResult.Fail($"No existe el entorno «{environmentId}».");
        if (index < 0 || index >= env.Links.Count) return CommandResult.Fail("Índice de enlace fuera de rango.");

        var links = env.Links.ToList();
        links.RemoveAt(index);
        var updated = env with { Links = links };
        _store.Save(s with { FocusEnvironments = s.FocusEnvironments.Select(e => e.Id == environmentId ? updated : e).ToList() });
        return CommandResult.Ok("Enlace eliminado.");
    }

    // ---------- Perfiles de apertura por tipo de sesión (#116) ----------

    /// <summary>
    /// Fija qué enlaces (URLs) y apps (procesos) se abren para un tipo de sesión
    /// (por título) dentro de un entorno. Reemplaza el perfil previo de ese título.
    /// </summary>
    public CommandResult SetSessionProfile(string environmentId, string sessionTitle,
        IReadOnlyList<string> enabledLinks, IReadOnlyList<string> enabledApps)
    {
        if (string.IsNullOrWhiteSpace(sessionTitle)) return CommandResult.Fail("Falta el título de la sesión.");
        var title = sessionTitle.Trim();
        return MutateEnvironment(environmentId, env =>
        {
            var others = env.SessionProfiles.Where(p => !string.Equals(p.SessionTitle.Trim(), title, StringComparison.OrdinalIgnoreCase)).ToList();
            others.Add(new SessionAppProfile
            {
                SessionTitle = title,
                EnabledLinks = enabledLinks.ToList(),
                EnabledApps = enabledApps.ToList()
            });
            return env with { SessionProfiles = others };
        }, "Comportamiento de la sesión actualizado.");
    }

    /// <summary>Olvida el perfil de un tipo de sesión (vuelve a "abrir todo").</summary>
    public CommandResult ClearSessionProfile(string environmentId, string sessionTitle)
    {
        var title = (sessionTitle ?? "").Trim();
        return MutateEnvironment(environmentId, env => env with
        {
            SessionProfiles = env.SessionProfiles
                .Where(p => !string.Equals(p.SessionTitle.Trim(), title, StringComparison.OrdinalIgnoreCase)).ToList()
        }, "Comportamiento de la sesión restablecido.");
    }

    // ---------- Tareas por entorno (#77) ----------

    private CommandResult MutateEnvironment(string environmentId, Func<FocusEnvironment, FocusEnvironment> change, string okMsg)
    {
        var s = _store.Load();
        var env = s.FocusEnvironments.FirstOrDefault(e => e.Id == environmentId);
        if (env is null) return CommandResult.Fail($"No existe el entorno «{environmentId}».");
        var updated = change(env);
        _store.Save(s with { FocusEnvironments = s.FocusEnvironments.Select(e => e.Id == environmentId ? updated : e).ToList() });
        return CommandResult.Ok(okMsg);
    }

    /// <summary>Añade una tarea al entorno. Devuelve su Id en el mensaje.</summary>
    public CommandResult AddEnvironmentTask(string environmentId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return CommandResult.Fail("La tarea necesita texto.");
        var id = $"task-{Guid.NewGuid():N}"[..12];
        return MutateEnvironment(environmentId, env =>
        {
            var order = env.Tasks.Count == 0 ? 0 : env.Tasks.Max(t => t.Order) + 1;
            return env with { Tasks = [.. env.Tasks, new EnvironmentTask { Id = id, Text = text.Trim(), Order = order }] };
        }, id);
    }

    /// <summary>Marca/desmarca una tarea como hecha.</summary>
    public CommandResult ToggleEnvironmentTask(string environmentId, string taskId)
    {
        var s = _store.Load();
        var env = s.FocusEnvironments.FirstOrDefault(e => e.Id == environmentId);
        if (env is null) return CommandResult.Fail($"No existe el entorno «{environmentId}».");
        if (env.Tasks.All(t => t.Id != taskId)) return CommandResult.Fail("No existe la tarea.");
        return MutateEnvironment(environmentId, e =>
            e with { Tasks = e.Tasks.Select(t => t.Id == taskId ? t with { Done = !t.Done } : t).ToList() },
            "Tarea actualizada.");
    }

    /// <summary>Elimina una tarea del entorno.</summary>
    public CommandResult RemoveEnvironmentTask(string environmentId, string taskId)
    {
        var s = _store.Load();
        var env = s.FocusEnvironments.FirstOrDefault(e => e.Id == environmentId);
        if (env is null) return CommandResult.Fail($"No existe el entorno «{environmentId}».");
        if (env.Tasks.All(t => t.Id != taskId)) return CommandResult.Fail("No existe la tarea.");
        return MutateEnvironment(environmentId, e =>
            e with { Tasks = e.Tasks.Where(t => t.Id != taskId).ToList() }, "Tarea eliminada.");
    }

    /// <summary>
    /// Mueve una tarea una posición arriba (<paramref name="up"/>=true) o abajo dentro del
    /// entorno. Reasigna <c>Order</c> de forma contigua (0..n-1). No-op si ya está en el borde.
    /// </summary>
    public CommandResult MoveEnvironmentTask(string environmentId, string taskId, bool up)
    {
        var s = _store.Load();
        var env = s.FocusEnvironments.FirstOrDefault(e => e.Id == environmentId);
        if (env is null) return CommandResult.Fail($"No existe el entorno «{environmentId}».");
        var ordered = env.Tasks.OrderBy(t => t.Order).ToList();
        var idx = ordered.FindIndex(t => t.Id == taskId);
        if (idx < 0) return CommandResult.Fail("No existe la tarea.");

        var target = up ? idx - 1 : idx + 1;
        if (target < 0 || target >= ordered.Count) return CommandResult.Ok("Sin cambios.");

        (ordered[idx], ordered[target]) = (ordered[target], ordered[idx]);
        return MutateEnvironment(environmentId, e =>
            e with { Tasks = ordered.Select((t, i) => t with { Order = i }).ToList() },
            "Tareas reordenadas.");
    }

    /// <summary>Fija el entorno por defecto (debe existir).</summary>
    public CommandResult SetDefaultEnvironment(string? environmentId)
    {
        var s = _store.Load();
        if (string.IsNullOrEmpty(environmentId))   // limpiar la selección (modo automático)
        {
            _store.Save(s with { DefaultFocusEnvironmentId = null });
            return CommandResult.Ok("Sin entorno por defecto.");
        }
        if (s.FocusEnvironments.All(e => e.Id != environmentId))
            return CommandResult.Fail($"No existe el entorno con id «{environmentId}».");
        _store.Save(s with { DefaultFocusEnvironmentId = environmentId });
        return CommandResult.Ok("Entorno por defecto actualizado.");
    }

    /// <summary>Guarda la conexión GLOBAL a Navidrome (servidor + usuario). La
    /// contraseña se guarda aparte en el almacén seguro del host. #107</summary>
    public CommandResult SetNavidromeConnection(string serverUrl, string user)
    {
        if (string.IsNullOrWhiteSpace(serverUrl)) return CommandResult.Fail("Falta la URL del servidor.");
        if (string.IsNullOrWhiteSpace(user)) return CommandResult.Fail("Falta el usuario.");
        var s = _store.Load();
        _store.Save(s with { NavidromeServerUrl = serverUrl.Trim(), NavidromeUser = user.Trim() });
        return CommandResult.Ok("Conexión de Navidrome guardada.");
    }

    /// <summary>Elimina la conexión global a Navidrome.</summary>
    public CommandResult ClearNavidromeConnection()
    {
        var s = _store.Load();
        _store.Save(s with { NavidromeServerUrl = null, NavidromeUser = null });
        return CommandResult.Ok("Conexión de Navidrome eliminada.");
    }

    /// <summary>
    /// Configura las notificaciones push al móvil vía ntfy (#122). Si <paramref name="enabled"/>
    /// es true, el topic es obligatorio y el servidor debe ser una URL http/https (vacío =
    /// ntfy.sh por defecto). El topic actúa como secreto compartido con el móvil.
    /// </summary>
    public CommandResult SetNtfy(bool enabled, string? serverUrl, string? topic)
    {
        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(topic))
                return CommandResult.Fail("Para activar las notificaciones al móvil necesitas un topic de ntfy.");
            var server = Notifications.NtfyPublish.NormalizeServer(serverUrl);
            if (!server.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return CommandResult.Fail("El servidor de ntfy debe ser una URL http/https.");
        }
        var s = _store.Load();
        _store.Save(s with
        {
            NtfyEnabled = enabled,
            NtfyServerUrl = string.IsNullOrWhiteSpace(serverUrl) ? null : serverUrl.Trim(),
            NtfyTopic = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim()
        });
        return CommandResult.Ok(enabled ? "Notificaciones al móvil activadas." : "Notificaciones al móvil desactivadas.");
    }

    // ---------- Suscripciones de calendario (ICS, #112) ----------

    /// <summary>Añade una suscripción a un calendario externo por enlace ICS. Devuelve su Id.</summary>
    public CommandResult AddCalendarFeed(string name, string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return CommandResult.Fail("Falta el enlace del calendario.");
        var u = url.Trim();
        if (!u.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !u.StartsWith("webcal", StringComparison.OrdinalIgnoreCase))
            return CommandResult.Fail("El enlace debe ser una URL (http/https/webcal).");
        var s = _store.Load();
        var feed = new CalendarFeed
        {
            Id = $"cal-{Guid.NewGuid():N}"[..12],
            Name = string.IsNullOrWhiteSpace(name) ? "Calendario" : name.Trim(),
            Url = u
        };
        _store.Save(s with { CalendarFeeds = [.. s.CalendarFeeds, feed] });
        return CommandResult.Ok(feed.Id);
    }

    /// <summary>Elimina una suscripción de calendario por Id.</summary>
    public CommandResult RemoveCalendarFeed(string id)
    {
        var s = _store.Load();
        if (s.CalendarFeeds.All(f => f.Id != id)) return CommandResult.Fail($"No existe el calendario «{id}».");
        _store.Save(s with { CalendarFeeds = s.CalendarFeeds.Where(f => f.Id != id).ToList() });
        return CommandResult.Ok("Calendario eliminado.");
    }

    // ---------- Prioridad en solapamientos horario↔calendario (#114) ----------

    /// <summary>
    /// Recuerda qué lado prioriza el usuario para un evento en conflicto con una
    /// sesión. Reemplaza cualquier decisión previa del mismo evento.
    /// </summary>
    public CommandResult SetOverlapPriority(string eventKey, bool preferCalendar)
    {
        if (string.IsNullOrWhiteSpace(eventKey)) return CommandResult.Fail("Falta el evento del solapamiento.");
        var s = _store.Load();
        var others = s.OverlapPriorities.Where(p => p.EventKey != eventKey).ToList();
        others.Add(new OverlapPriority { EventKey = eventKey, PreferCalendar = preferCalendar });
        _store.Save(s with { OverlapPriorities = others });
        return CommandResult.Ok(preferCalendar ? "Priorizado el evento del calendario." : "Priorizada la sesión.");
    }

    /// <summary>Olvida la decisión de prioridad de un evento (vuelve a "sin decidir").</summary>
    public CommandResult ClearOverlapPriority(string eventKey)
    {
        var s = _store.Load();
        if (s.OverlapPriorities.All(p => p.EventKey != eventKey)) return CommandResult.Ok("Sin cambios.");
        _store.Save(s with { OverlapPriorities = s.OverlapPriorities.Where(p => p.EventKey != eventKey).ToList() });
        return CommandResult.Ok("Prioridad eliminada.");
    }

    /// <summary>Asocia una categoría de bloque a un entorno (debe existir).</summary>
    public CommandResult MapEnvironmentToKind(string categoryId, string environmentId)
    {
        var s = _store.Load();
        if (string.IsNullOrWhiteSpace(categoryId)) return CommandResult.Fail("Categoría inválida.");
        if (s.FocusEnvironments.All(e => e.Id != environmentId))
            return CommandResult.Fail($"No existe el entorno con id «{environmentId}».");

        var map = new Dictionary<string, string>(s.EnvironmentByKind) { [categoryId] = environmentId };
        _store.Save(s with { EnvironmentByKind = map });
        return CommandResult.Ok($"Categoría «{categoryId}» asociada al entorno «{environmentId}».");
    }

    /// <summary>Quita la asociación categoría→entorno: esa categoría vuelve a usar el predeterminado. #70</summary>
    public CommandResult ClearEnvironmentKind(string categoryId)
    {
        var s = _store.Load();
        if (!s.EnvironmentByKind.ContainsKey(categoryId)) return CommandResult.Ok("Sin cambios.");
        var map = s.EnvironmentByKind.Where(kv => kv.Key != categoryId).ToDictionary(kv => kv.Key, kv => kv.Value);
        _store.Save(s with { EnvironmentByKind = map });
        return CommandResult.Ok($"Categoría «{categoryId}» usa el entorno predeterminado.");
    }

    // ---------- Categorías de bloque configurables (#83) ----------

    /// <summary>Crea una categoría. Genera un id (slug). Devuelve su id en el mensaje.</summary>
    public CommandResult AddCategory(string name, string colorHex, bool isFocus)
    {
        if (string.IsNullOrWhiteSpace(name)) return CommandResult.Fail("La categoría necesita un nombre.");
        var norm = NormalizeHexColor(colorHex);
        if (norm is null) return CommandResult.Fail("Color inválido. Usa el formato #RRGGBB.");
        var s = _store.Load();
        var id = Ritmo.Core.Model.CategorySlug.From(name, s.Categories.Select(c => c.Id));
        var order = s.Categories.Count == 0 ? 0 : s.Categories.Max(c => c.Order) + 1;
        var cat = new Ritmo.Core.Model.BlockCategory
        {
            Id = id, Name = name.Trim(), ColorHex = norm, IsFocus = isFocus, Order = order
        };
        _store.Save(s with { Categories = [.. s.Categories, cat] });
        return CommandResult.Ok(id);
    }

    /// <summary>Actualiza nombre/color/focus de una categoría (no cambia su id).</summary>
    public CommandResult UpdateCategory(string id, string name, string colorHex, bool isFocus)
    {
        if (string.IsNullOrWhiteSpace(name)) return CommandResult.Fail("La categoría necesita un nombre.");
        var norm = NormalizeHexColor(colorHex);
        if (norm is null) return CommandResult.Fail("Color inválido. Usa el formato #RRGGBB.");
        var s = _store.Load();
        if (s.Categories.All(c => c.Id != id)) return CommandResult.Fail($"No existe la categoría «{id}».");
        var updated = s.Categories
            .Select(c => c.Id == id ? c with { Name = name.Trim(), ColorHex = norm, IsFocus = isFocus } : c)
            .ToList();
        _store.Save(s with { Categories = updated });
        return CommandResult.Ok("Categoría actualizada.");
    }

    /// <summary>
    /// Elimina una categoría (las de sistema no se pueden borrar). Reasigna a «Otro» las
    /// sesiones, sesiones provisionales y mapeos de entorno que la usaban.
    /// </summary>
    public CommandResult RemoveCategory(string id)
    {
        var s = _store.Load();
        var cat = s.Categories.FirstOrDefault(c => c.Id == id);
        if (cat is null) return CommandResult.Fail($"No existe la categoría «{id}».");
        if (cat.IsSystem) return CommandResult.Fail("No se puede borrar una categoría de sistema.");

        StudySession Fix(StudySession x) => x.CategoryId == id
            ? x with { CategoryId = Ritmo.Core.Model.CategoryIds.Other } : x;

        var newSchedule = s.Schedule with { Sessions = s.Schedule.Sessions.Select(Fix).ToList() };
        var newPhases = s.Plan.Phases
            .Select(p => p with { Schedule = p.Schedule with { Sessions = p.Schedule.Sessions.Select(Fix).ToList() } })
            .ToList();
        var newOneOff = s.OneOffSessions
            .Select(o => o.CategoryId == id ? o with { CategoryId = Ritmo.Core.Model.CategoryIds.Other } : o)
            .ToList();
        var newEnvMap = s.EnvironmentByKind.Where(kv => kv.Key != id).ToDictionary(kv => kv.Key, kv => kv.Value);
        var newCats = s.Categories.Where(c => c.Id != id)
            .OrderBy(c => c.Order).Select((c, i) => c with { Order = i }).ToList();

        _store.Save(s with
        {
            Schedule = newSchedule,
            Plan = s.Plan with { Phases = newPhases },
            OneOffSessions = newOneOff,
            EnvironmentByKind = newEnvMap,
            Categories = newCats
        });
        return CommandResult.Ok($"Categoría «{cat.Name}» eliminada; sus bloques pasan a «Otro».");
    }

    /// <summary>Mueve una categoría una posición arriba/abajo. Reasigna Order contiguo (0..n-1).</summary>
    public CommandResult ReorderCategory(string id, bool up)
    {
        var s = _store.Load();
        var ordered = s.Categories.OrderBy(c => c.Order).ToList();
        var idx = ordered.FindIndex(c => c.Id == id);
        if (idx < 0) return CommandResult.Fail($"No existe la categoría «{id}».");
        var target = up ? idx - 1 : idx + 1;
        if (target < 0 || target >= ordered.Count) return CommandResult.Ok("Sin cambios.");
        (ordered[idx], ordered[target]) = (ordered[target], ordered[idx]);
        _store.Save(s with { Categories = ordered.Select((c, i) => c with { Order = i }).ToList() });
        return CommandResult.Ok("Categorías reordenadas.");
    }

    /// <summary>Fija/restablece el color de una categoría (alias claro de SetKindColor). #83</summary>
    public CommandResult SetCategoryColor(string id, string? hex) => SetKindColor(id, hex);

    /// <summary>
    /// Siembra el set de categorías de una plantilla del onboarding ("estudio"/"trabajo"/
    /// "blanco") y marca el onboarding como completado. Reemplaza las categorías actuales.
    /// </summary>
    public CommandResult SeedTemplate(string templateId)
    {
        var s = _store.Load();
        var cats = Ritmo.Core.Model.CategoryDefaults.ForTemplate(templateId);
        _store.Save(s with { Categories = cats, OnboardingCompleted = true });
        return CommandResult.Ok($"Plantilla «{templateId}» aplicada.");
    }
}

/// <summary>Resumen del estado de la app (respuesta para IA / UI).</summary>
public sealed record StatusReport
{
    public int PhaseCount { get; init; }
    public IReadOnlyList<string> PhaseNames { get; init; } = [];
    public int EnvironmentCount { get; init; }
    public IReadOnlyList<string> EnvironmentNames { get; init; } = [];
    public string? DefaultEnvironmentId { get; init; }
    public int NoteCount { get; init; }
}
