using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;

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

        var phase = new SchedulePhase { Name = name, ValidFrom = validFrom, ValidTo = validTo };
        var updated = s with { Plan = new SchedulePlan { Phases = [.. s.Plan.Phases, phase] } };
        _store.Save(updated);
        return CommandResult.Ok($"Fase «{name}» añadida.");
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

    /// <summary>Actualiza el rango horario visible de la rejilla del horario.</summary>
    public CommandResult SetViewHours(TimeOnly dayStart, TimeOnly dayEnd)
    {
        if (dayEnd <= dayStart) return CommandResult.Fail("La hora de fin debe ser posterior a la de inicio.");
        var s = _store.Load();
        _store.Save(s with { ViewConfig = s.ViewConfig with { DayStart = dayStart, DayEnd = dayEnd } });
        return CommandResult.Ok("Rango horario actualizado.");
    }

    // ---------- Notas y enlaces-atajo (#55) ----------

    /// <summary>Añade una nota fijada (markdown). Devuelve su Id en el mensaje.</summary>
    public CommandResult AddNote(string title, string content, string? accentColor = null)
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
            Order = order
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

    /// <summary>Fija el entorno por defecto (debe existir).</summary>
    public CommandResult SetDefaultEnvironment(string environmentId)
    {
        var s = _store.Load();
        if (s.FocusEnvironments.All(e => e.Id != environmentId))
            return CommandResult.Fail($"No existe el entorno con id «{environmentId}».");
        _store.Save(s with { DefaultFocusEnvironmentId = environmentId });
        return CommandResult.Ok("Entorno por defecto actualizado.");
    }

    /// <summary>Asocia un tipo de bloque a un entorno (debe existir).</summary>
    public CommandResult MapEnvironmentToKind(StudyKind kind, string environmentId)
    {
        var s = _store.Load();
        if (s.FocusEnvironments.All(e => e.Id != environmentId))
            return CommandResult.Fail($"No existe el entorno con id «{environmentId}».");

        var map = new Dictionary<StudyKind, string>(s.EnvironmentByKind) { [kind] = environmentId };
        _store.Save(s with { EnvironmentByKind = map });
        return CommandResult.Ok($"Tipo {kind} asociado al entorno «{environmentId}».");
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
