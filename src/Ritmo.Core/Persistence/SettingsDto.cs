using System.Globalization;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo.Core.Pomodoro;

namespace Ritmo.Core.Persistence;

// DTOs de almacenamiento: representación JSON-friendly y editable a mano.
// Horas como "HH:mm", fechas como "yyyy-MM-dd", duraciones en minutos.

internal sealed class SettingsDto
{
    // Compatibilidad: horario suelto del formato original.
    public List<SessionDto> Sessions { get; set; } = [];
    public PomodoroDto Pomodoro { get; set; } = new();
    public List<PomodoroRhythmDto> Rhythms { get; set; } = [];
    // Nuevo: horario por fases, notas y configuración de vista.
    public List<PhaseDto> Phases { get; set; } = [];
    public List<OneOffSessionDto> OneOffSessions { get; set; } = [];
    public List<NoteDto> Notes { get; set; } = [];
    public ViewConfigDto? ViewConfig { get; set; }
    public List<FocusEnvironmentDto> FocusEnvironments { get; set; } = [];
    public string? DefaultFocusEnvironmentId { get; set; }
    public Dictionary<string, string> EnvironmentByKind { get; set; } = [];
    // Categorías de bloque definibles (#83). Vacío en JSON legacy → la migración las deriva.
    public List<CategoryDto> Categories { get; set; } = [];
    public bool OnboardingCompleted { get; set; }
    // Modo descanso (#135): manual + periodos programados.
    public bool RestActive { get; set; }
    public List<RestPeriodDto> RestPeriods { get; set; } = [];
    // Seguimiento laboral (#84 V3): proyectos + registro de horas.
    public List<WorkProjectDto> WorkProjects { get; set; } = [];
    public List<WorkLogEntryDto> WorkLog { get; set; } = [];
    public List<SessionExceptionDto>? SessionExceptions { get; set; }   // #137
    // LEGACY (#84 V1/V2): tarifa/objetivo por ENTORNO. Se migran a proyectos en FromDto y dejan de
    // escribirse. Se mantienen solo para leer settings.json antiguos.
    public Dictionary<string, decimal>? EnvironmentRates { get; set; }
    public Dictionary<string, double>? EnvironmentGoals { get; set; }
    public string? NavidromeServerUrl { get; set; }
    public string? NavidromeUser { get; set; }
    public bool NtfyEnabled { get; set; }
    public string? NtfyServerUrl { get; set; }
    public string? NtfyTopic { get; set; }
    public string? LastSeenVersion { get; set; }
    public List<CalendarFeedDto> CalendarFeeds { get; set; } = [];
    public List<OverlapPriorityDto> OverlapPriorities { get; set; } = [];
}

internal sealed class CalendarFeedDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

internal sealed class RestPeriodDto
{
    public string Id { get; set; } = "";
    public string From { get; set; } = "2026-01-01";
    public string To { get; set; } = "2026-01-01";
    public string Label { get; set; } = "";
}

internal sealed class WorkProjectDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "#1E88E5";
    public decimal Rate { get; set; }
    public double MonthlyGoalHours { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public int Order { get; set; }
    public bool Archived { get; set; }
    public bool AutoFromSchedule { get; set; } = true;   // #137
}

internal sealed class SessionExceptionDto
{
    public string Id { get; set; } = "";
    public string SessionKey { get; set; } = "";
    public string From { get; set; } = "2026-01-01";
    public string To { get; set; } = "2026-01-01";
    public string Reason { get; set; } = "";
}

internal sealed class WorkLogEntryDto
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string? EnvironmentId { get; set; }   // LEGACY (#84 V1/V2): migrado a ProjectId.
    public string Date { get; set; } = "2026-01-01";
    public double Hours { get; set; }
    public string Note { get; set; } = "";
}

internal sealed class CategoryDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "#EDEDED";
    public string? TextColorHex { get; set; }
    public bool IsFocus { get; set; }
    public int Order { get; set; }
    public bool IsSystem { get; set; }
}

internal sealed class OverlapPriorityDto
{
    public string EventKey { get; set; } = "";
    public bool PreferCalendar { get; set; }
}

internal sealed class MusicDto
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Arguments { get; set; }
    public bool AutoPlay { get; set; }
    // Proveedor configurable (Navidrome, #107). Servidor/usuario son globales.
    public string? Provider { get; set; }
    public string? PlaylistId { get; set; }
    public string? PlaylistName { get; set; }
}

internal sealed class FocusEnvironmentDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? PomodoroPreset { get; set; }
    public bool EnableDoNotDisturb { get; set; } = true;
    public bool HideTaskbarBadges { get; set; } = true;
    public bool ShowDayPreview { get; set; } = true;
    public bool OpenLinksInBrowser { get; set; }
    public List<string> BlockedWebsites { get; set; } = [];
    public List<string> AppsToClose { get; set; } = [];
    public List<string> AppsToMute { get; set; } = [];
    public List<string> AppsToOpen { get; set; } = [];
    public bool NewVirtualDesktop { get; set; }
    public MusicDto? Music { get; set; }
    public List<ShortcutDto> Links { get; set; } = [];
    public List<EnvTaskDto> Tasks { get; set; } = [];
    public List<SessionAppProfileDto> SessionProfiles { get; set; } = [];
}

internal sealed class SessionAppProfileDto
{
    public string SessionTitle { get; set; } = "";
    public List<string> EnabledLinks { get; set; } = [];
    public List<string> EnabledApps { get; set; } = [];
}

internal sealed class EnvTaskDto
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Done { get; set; }
    public int Order { get; set; }
}

internal sealed class SessionDto
{
    public string Title { get; set; } = "";
    public string Day { get; set; } = "Monday";
    public string Start { get; set; } = "09:00";
    public int DurationMinutes { get; set; } = 60;
    public string Kind { get; set; } = "Otro";
    public List<int> PreAlertsMinutes { get; set; } = [];
    public bool IsTentative { get; set; }
    public string? ProjectId { get; set; }   // #137: vínculo a proyecto de seguimiento laboral
}

internal sealed class PomodoroDto
{
    public int FocusMinutes { get; set; } = 50;
    public int ShortBreakMinutes { get; set; } = 10;
    public int LongBreakMinutes { get; set; } = 20;
    public int FocusesPerLongBreak { get; set; } = 2;
}

internal sealed class PomodoroRhythmDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int FocusMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int FocusesPerLongBreak { get; set; } = 4;
}

internal sealed class OneOffSessionDto
{
    public string Id { get; set; } = "";
    public string Date { get; set; } = "2026-01-01";
    public string Title { get; set; } = "";
    public string Start { get; set; } = "09:00";
    public int DurationMinutes { get; set; } = 60;
    public string Kind { get; set; } = "Otro";
    public List<int> PreAlertsMinutes { get; set; } = [];
    public bool IsTentative { get; set; }
}

internal sealed class PhaseDto
{
    public string Name { get; set; } = "";
    public string ValidFrom { get; set; } = "2026-01-01";
    public string? ValidTo { get; set; }
    public List<SessionDto> Sessions { get; set; } = [];
}

internal sealed class NoteDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? AccentColor { get; set; }
    public int Order { get; set; }
    public string? SessionTitle { get; set; }
}

internal sealed class ShortcutDto
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
}

internal sealed class ViewConfigDto
{
    public string DayStart { get; set; } = "08:00";
    public string DayEnd { get; set; } = "20:00";
    public Dictionary<string, string> ColorsByKind { get; set; } = [];
    public List<ShortcutDto> Shortcuts { get; set; } = [];
    public bool ShowDayPreviewOnFocusStart { get; set; } = true;
    public int GranularityMinutes { get; set; } = 60;
    public int DefaultPreAlertMinutes { get; set; } = 10;
}

/// <summary>Conversión entre el modelo de dominio y los DTO de almacenamiento.</summary>
internal static class SettingsMapper
{
    private const string TimeFormat = "HH\\:mm";
    private const string DateFormat = "yyyy-MM-dd";

    // ---------- Dominio -> DTO ----------
    public static SettingsDto ToDto(AppSettings s) => new()
    {
        Sessions = s.Schedule.Sessions.Select(ToDto).ToList(),
        Pomodoro = new PomodoroDto
        {
            FocusMinutes = (int)s.Pomodoro.Focus.TotalMinutes,
            ShortBreakMinutes = (int)s.Pomodoro.ShortBreak.TotalMinutes,
            LongBreakMinutes = (int)s.Pomodoro.LongBreak.TotalMinutes,
            FocusesPerLongBreak = s.Pomodoro.FocusesPerLongBreak
        },
        Rhythms = s.Rhythms.Where(r => !r.IsBuiltIn).Select(r => new PomodoroRhythmDto
        {
            Id = r.Id, Name = r.Name,
            FocusMinutes = r.FocusMinutes, ShortBreakMinutes = r.ShortBreakMinutes,
            LongBreakMinutes = r.LongBreakMinutes, FocusesPerLongBreak = r.FocusesPerLongBreak
        }).ToList(),
        Phases = s.Plan.Phases.Select(ToDto).ToList(),
        OneOffSessions = s.OneOffSessions.Select(o => new OneOffSessionDto
        {
            Id = o.Id,
            Date = o.Date.ToString(DateFormat, CultureInfo.InvariantCulture),
            Title = o.Title,
            Start = o.Start.ToString(TimeFormat, CultureInfo.InvariantCulture),
            DurationMinutes = (int)o.Duration.TotalMinutes,
            Kind = o.CategoryId,
            PreAlertsMinutes = o.PreAlerts.Select(a => a.MinutesBefore).ToList(),
            IsTentative = o.IsTentative
        }).ToList(),
        Notes = s.Notes.Select(ToDto).ToList(),
        ViewConfig = ToDto(s.ViewConfig),
        FocusEnvironments = s.FocusEnvironments.Select(ToDto).ToList(),
        DefaultFocusEnvironmentId = s.DefaultFocusEnvironmentId,
        EnvironmentByKind = s.EnvironmentByKind.ToDictionary(kv => kv.Key, kv => kv.Value),
        Categories = s.Categories.Select(c => new CategoryDto
        {
            Id = c.Id, Name = c.Name, ColorHex = c.ColorHex, TextColorHex = c.TextColorHex,
            IsFocus = c.IsFocus, Order = c.Order, IsSystem = c.IsSystem
        }).ToList(),
        OnboardingCompleted = s.OnboardingCompleted,
        RestActive = s.RestActive,
        RestPeriods = s.RestPeriods.Select(p => new RestPeriodDto
        {
            Id = p.Id,
            From = p.From.ToString(DateFormat, CultureInfo.InvariantCulture),
            To = p.To.ToString(DateFormat, CultureInfo.InvariantCulture),
            Label = p.Label
        }).ToList(),
        // #84 V3: solo se escriben proyectos; los mapas legacy por entorno ya no se persisten.
        WorkProjects = s.WorkProjects.Select(p => new WorkProjectDto
        {
            Id = p.Id, Name = p.Name, ColorHex = p.ColorHex, Rate = p.Rate,
            MonthlyGoalHours = p.MonthlyGoalHours, CurrencyCode = p.CurrencyCode,
            Order = p.Order, Archived = p.Archived, AutoFromSchedule = p.AutoFromSchedule
        }).ToList(),
        WorkLog = s.WorkLog.Select(w => new WorkLogEntryDto
        {
            Id = w.Id,
            ProjectId = w.ProjectId,
            Date = w.Date.ToString(DateFormat, CultureInfo.InvariantCulture),
            Hours = w.Hours,
            Note = w.Note
        }).ToList(),
        SessionExceptions = s.SessionExceptions.Select(x => new SessionExceptionDto
        {
            Id = x.Id, SessionKey = x.SessionKey,
            From = x.From.ToString(DateFormat, CultureInfo.InvariantCulture),
            To = x.To.ToString(DateFormat, CultureInfo.InvariantCulture),
            Reason = x.Reason
        }).ToList(),
        NavidromeServerUrl = s.NavidromeServerUrl,
        NavidromeUser = s.NavidromeUser,
        NtfyEnabled = s.NtfyEnabled,
        NtfyServerUrl = s.NtfyServerUrl,
        NtfyTopic = s.NtfyTopic,
        LastSeenVersion = s.LastSeenVersion,
        CalendarFeeds = s.CalendarFeeds.Select(f => new CalendarFeedDto { Id = f.Id, Name = f.Name, Url = f.Url }).ToList(),
        OverlapPriorities = s.OverlapPriorities
            .Select(p => new OverlapPriorityDto { EventKey = p.EventKey, PreferCalendar = p.PreferCalendar }).ToList()
    };

    private static FocusEnvironmentDto ToDto(FocusEnvironment e) => new()
    {
        Id = e.Id, Name = e.Name, PomodoroPreset = e.PomodoroPreset,
        EnableDoNotDisturb = e.EnableDoNotDisturb, HideTaskbarBadges = e.HideTaskbarBadges,
        ShowDayPreview = e.ShowDayPreview, OpenLinksInBrowser = e.OpenLinksInBrowser,
        BlockedWebsites = e.BlockedWebsites.ToList(),
        AppsToClose = e.AppsToClose.ToList(),
        AppsToMute = e.AppsToMute.ToList(),
        AppsToOpen = e.AppsToOpen.ToList(),
        NewVirtualDesktop = e.NewVirtualDesktop,
        Music = e.Music is null ? null : new MusicDto
        {
            Name = e.Music.Name, Target = e.Music.Target,
            Arguments = e.Music.Arguments, AutoPlay = e.Music.AutoPlay,
            Provider = e.Music.Provider,
            PlaylistId = e.Music.PlaylistId, PlaylistName = e.Music.PlaylistName
        },
        Links = e.Links.Select(l => new ShortcutDto { Title = l.Title, Url = l.Url }).ToList(),
        Tasks = e.Tasks.Select(t => new EnvTaskDto { Id = t.Id, Text = t.Text, Done = t.Done, Order = t.Order }).ToList(),
        SessionProfiles = e.SessionProfiles.Select(p => new SessionAppProfileDto
        {
            SessionTitle = p.SessionTitle,
            EnabledLinks = p.EnabledLinks.ToList(),
            EnabledApps = p.EnabledApps.ToList()
        }).ToList()
    };

    private static SessionDto ToDto(StudySession x) => new()
    {
        Title = x.Title,
        Day = x.Day.ToString(),
        Start = x.Start.ToString(TimeFormat, CultureInfo.InvariantCulture),
        DurationMinutes = (int)x.Duration.TotalMinutes,
        Kind = x.CategoryId,
        PreAlertsMinutes = x.PreAlerts.Select(a => a.MinutesBefore).ToList(),
        IsTentative = x.IsTentative,
        ProjectId = x.ProjectId
    };

    private static PhaseDto ToDto(SchedulePhase p) => new()
    {
        Name = p.Name,
        ValidFrom = p.ValidFrom.ToString(DateFormat, CultureInfo.InvariantCulture),
        ValidTo = p.ValidTo?.ToString(DateFormat, CultureInfo.InvariantCulture),
        Sessions = p.Schedule.Sessions.Select(ToDto).ToList()
    };

    private static NoteDto ToDto(StudyNote n) => new()
    {
        Id = n.Id, Title = n.Title, Content = n.Content,
        AccentColor = n.AccentColor, Order = n.Order, SessionTitle = n.SessionTitle
    };

    private static ViewConfigDto ToDto(ScheduleViewConfig v) => new()
    {
        DayStart = v.DayStart.ToString(TimeFormat, CultureInfo.InvariantCulture),
        DayEnd = v.DayEnd.ToString(TimeFormat, CultureInfo.InvariantCulture),
        // ColorsByKind es legacy (#83): el color vive ahora en BlockCategory. No se escribe.
        ColorsByKind = [],
        Shortcuts = v.Shortcuts.Select(s => new ShortcutDto { Title = s.Title, Url = s.Url }).ToList(),
        ShowDayPreviewOnFocusStart = v.ShowDayPreviewOnFocusStart,
        GranularityMinutes = v.GranularityMinutes,
        DefaultPreAlertMinutes = v.DefaultPreAlertMinutes
    };

    // ---------- DTO -> Dominio ----------
    public static AppSettings FromDto(SettingsDto d)
    {
        var s = new AppSettings
        {
        Schedule = new WeeklySchedule { Sessions = d.Sessions.Select(FromDto).ToList() },
        Pomodoro = new PomodoroConfig(
            TimeSpan.FromMinutes(d.Pomodoro.FocusMinutes),
            TimeSpan.FromMinutes(d.Pomodoro.ShortBreakMinutes),
            TimeSpan.FromMinutes(d.Pomodoro.LongBreakMinutes),
            d.Pomodoro.FocusesPerLongBreak),
        Rhythms = d.Rhythms.Select(r => new PomodoroRhythm
        {
            Id = r.Id, Name = r.Name,
            FocusMinutes = r.FocusMinutes, ShortBreakMinutes = r.ShortBreakMinutes,
            LongBreakMinutes = r.LongBreakMinutes, FocusesPerLongBreak = r.FocusesPerLongBreak
        }).ToList(),
        Plan = new SchedulePlan { Phases = d.Phases.Select(FromDto).ToList() },
        OneOffSessions = d.OneOffSessions.Select(o => new OneOffSession
        {
            Id = string.IsNullOrWhiteSpace(o.Id) ? $"oneoff-{Guid.NewGuid():N}"[..14] : o.Id,
            Date = DateOnly.ParseExact(o.Date, DateFormat, CultureInfo.InvariantCulture),
            Title = o.Title,
            Start = TimeOnly.ParseExact(o.Start, TimeFormat, CultureInfo.InvariantCulture),
            Duration = TimeSpan.FromMinutes(o.DurationMinutes),
            CategoryId = string.IsNullOrWhiteSpace(o.Kind) ? CategoryIds.Other : o.Kind,
            PreAlerts = o.PreAlertsMinutes.Select(m => new PreAlert(m)).ToList(),
            IsTentative = o.IsTentative
        }).ToList(),
        Notes = d.Notes.Select(FromDto).ToList(),
        ViewConfig = d.ViewConfig is null ? new ScheduleViewConfig() : FromDto(d.ViewConfig),
        FocusEnvironments = d.FocusEnvironments.Select(FromDto).ToList(),
        DefaultFocusEnvironmentId = d.DefaultFocusEnvironmentId,
        EnvironmentByKind = d.EnvironmentByKind
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value),
        Categories = d.Categories
            .Where(c => !string.IsNullOrWhiteSpace(c.Id))
            .Select(c => new BlockCategory
            {
                Id = c.Id, Name = string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name,
                ColorHex = c.ColorHex, TextColorHex = c.TextColorHex,
                IsFocus = c.IsFocus, Order = c.Order, IsSystem = c.IsSystem
            }).ToList(),
        OnboardingCompleted = d.OnboardingCompleted,
        RestActive = d.RestActive,
        RestPeriods = (d.RestPeriods ?? []).Select(p => new RestPeriod
        {
            Id = string.IsNullOrWhiteSpace(p.Id) ? $"rest-{Guid.NewGuid():N}"[..12] : p.Id,
            From = DateOnly.ParseExact(p.From, DateFormat, CultureInfo.InvariantCulture),
            To = DateOnly.ParseExact(p.To, DateFormat, CultureInfo.InvariantCulture),
            Label = p.Label ?? ""
        }).ToList(),
        WorkProjects = MigrateWorkProjects(d),
        WorkLog = MigrateWorkLog(d),
        NavidromeServerUrl = d.NavidromeServerUrl,
        NavidromeUser = d.NavidromeUser,
        NtfyEnabled = d.NtfyEnabled,
        NtfyServerUrl = d.NtfyServerUrl,
        NtfyTopic = d.NtfyTopic,
        LastSeenVersion = d.LastSeenVersion,
        CalendarFeeds = d.CalendarFeeds.Select(f => new CalendarFeed { Id = f.Id, Name = f.Name, Url = f.Url }).ToList(),
        OverlapPriorities = d.OverlapPriorities
            .Where(p => !string.IsNullOrWhiteSpace(p.EventKey))
            .Select(p => new OverlapPriority { EventKey = p.EventKey, PreferCalendar = p.PreferCalendar }).ToList(),
        SessionExceptions = (d.SessionExceptions ?? []).Select(x => new SessionException
        {
            Id = string.IsNullOrWhiteSpace(x.Id) ? $"exc-{Guid.NewGuid():N}"[..12] : x.Id,
            SessionKey = x.SessionKey ?? "",
            From = DateOnly.ParseExact(x.From, DateFormat, CultureInfo.InvariantCulture),
            To = DateOnly.ParseExact(x.To, DateFormat, CultureInfo.InvariantCulture),
            Reason = x.Reason ?? ""
        }).ToList()
        };
        return CategoryMigration.Apply(s, d.ViewConfig?.ColorsByKind);
    }

    /// <summary>
    /// Migración #84 V3: si el JSON trae proyectos nuevos, se usan. Si no, se DERIVAN de los datos
    /// legacy por entorno (EnvironmentRates/EnvironmentGoals + entornos referenciados en el WorkLog):
    /// un proyecto por cada entorno que tuviera tarifa, objetivo u horas anotadas, con id = id del
    /// entorno (así el WorkLog legacy, que guardaba EnvironmentId, sigue casando como ProjectId).
    /// </summary>
    private static List<WorkProject> MigrateWorkProjects(SettingsDto d)
    {
        if (d.WorkProjects is { Count: > 0 })
            return d.WorkProjects.Select(p => new WorkProject
            {
                Id = p.Id, Name = string.IsNullOrWhiteSpace(p.Name) ? p.Id : p.Name,
                ColorHex = string.IsNullOrWhiteSpace(p.ColorHex) ? "#1E88E5" : p.ColorHex,
                Rate = p.Rate, MonthlyGoalHours = p.MonthlyGoalHours,
                CurrencyCode = string.IsNullOrWhiteSpace(p.CurrencyCode) ? "EUR" : p.CurrencyCode,
                Order = p.Order, Archived = p.Archived, AutoFromSchedule = p.AutoFromSchedule
            }).ToList();

        // Legacy → derivar proyectos. Reúne todos los entornos con datos de seguimiento.
        var rates = d.EnvironmentRates ?? [];
        var goals = d.EnvironmentGoals ?? [];
        var loggedEnvs = (d.WorkLog ?? [])
            .Select(w => w.EnvironmentId).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!);
        var envIds = rates.Keys.Concat(goals.Keys).Concat(loggedEnvs)
            .Distinct().ToList();
        if (envIds.Count == 0) return [];

        var envNames = (d.FocusEnvironments ?? []).ToDictionary(e => e.Id, e => e.Name);
        int order = 0;
        return envIds.Select(id => new WorkProject
        {
            Id = id,   // mismo id que el entorno → el WorkLog legacy (EnvironmentId) casa como ProjectId
            Name = envNames.TryGetValue(id, out var n) && !string.IsNullOrWhiteSpace(n) ? n : id,
            Rate = rates.TryGetValue(id, out var r) ? r : 0,
            MonthlyGoalHours = goals.TryGetValue(id, out var g) ? g : 0,
            Order = order++
        }).ToList();
    }

    private static List<WorkLogEntry> MigrateWorkLog(SettingsDto d)
        => (d.WorkLog ?? []).Select(w => new WorkLogEntry
        {
            Id = string.IsNullOrWhiteSpace(w.Id) ? $"work-{Guid.NewGuid():N}"[..12] : w.Id,
            // V3 usa ProjectId; si falta (JSON legacy), cae al EnvironmentId (mismo valor que el id de proyecto migrado).
            ProjectId = string.IsNullOrWhiteSpace(w.ProjectId) ? (w.EnvironmentId ?? "") : w.ProjectId,
            Date = DateOnly.ParseExact(w.Date, DateFormat, CultureInfo.InvariantCulture),
            Hours = w.Hours,
            Note = w.Note ?? ""
        }).ToList();

    private static FocusEnvironment FromDto(FocusEnvironmentDto e) => new()
    {
        Id = e.Id, Name = e.Name, PomodoroPreset = e.PomodoroPreset,
        EnableDoNotDisturb = e.EnableDoNotDisturb, HideTaskbarBadges = e.HideTaskbarBadges,
        ShowDayPreview = e.ShowDayPreview, OpenLinksInBrowser = e.OpenLinksInBrowser,
        BlockedWebsites = e.BlockedWebsites.ToList(),
        AppsToClose = e.AppsToClose.ToList(),
        AppsToMute = e.AppsToMute.ToList(),
        AppsToOpen = e.AppsToOpen.ToList(),
        NewVirtualDesktop = e.NewVirtualDesktop,
        Music = e.Music is null ? null : new MusicLauncher
        {
            Name = e.Music.Name, Target = e.Music.Target,
            Arguments = e.Music.Arguments, AutoPlay = e.Music.AutoPlay,
            Provider = e.Music.Provider,
            PlaylistId = e.Music.PlaylistId, PlaylistName = e.Music.PlaylistName
        },
        Links = e.Links.Select(l => new ShortcutLink { Title = l.Title, Url = l.Url }).ToList(),
        Tasks = e.Tasks.Select(t => new EnvironmentTask { Id = t.Id, Text = t.Text, Done = t.Done, Order = t.Order }).ToList(),
        SessionProfiles = e.SessionProfiles
            .Where(p => !string.IsNullOrWhiteSpace(p.SessionTitle))
            .Select(p => new SessionAppProfile
            {
                SessionTitle = p.SessionTitle,
                EnabledLinks = p.EnabledLinks.ToList(),
                EnabledApps = p.EnabledApps.ToList()
            }).ToList()
    };

    private static StudySession FromDto(SessionDto x) => new()
    {
        Title = x.Title,
        Day = Enum.Parse<DayOfWeek>(x.Day, ignoreCase: true),
        Start = TimeOnly.ParseExact(x.Start, TimeFormat, CultureInfo.InvariantCulture),
        Duration = TimeSpan.FromMinutes(x.DurationMinutes),
        CategoryId = string.IsNullOrWhiteSpace(x.Kind) ? CategoryIds.Other : x.Kind,
        PreAlerts = x.PreAlertsMinutes.Select(m => new PreAlert(m)).ToList(),
        IsTentative = x.IsTentative,
        ProjectId = string.IsNullOrWhiteSpace(x.ProjectId) ? null : x.ProjectId
    };

    private static SchedulePhase FromDto(PhaseDto p) => new()
    {
        Name = p.Name,
        ValidFrom = DateOnly.ParseExact(p.ValidFrom, DateFormat, CultureInfo.InvariantCulture),
        ValidTo = string.IsNullOrWhiteSpace(p.ValidTo)
            ? null
            : DateOnly.ParseExact(p.ValidTo, DateFormat, CultureInfo.InvariantCulture),
        Schedule = new WeeklySchedule { Sessions = p.Sessions.Select(FromDto).ToList() }
    };

    private static StudyNote FromDto(NoteDto n) => new()
    {
        Id = n.Id, Title = n.Title, Content = n.Content,
        AccentColor = n.AccentColor, Order = n.Order,
        SessionTitle = string.IsNullOrWhiteSpace(n.SessionTitle) ? null : n.SessionTitle
    };

    private static ScheduleViewConfig FromDto(ViewConfigDto v) => new()
    {
        DayStart = TimeOnly.ParseExact(v.DayStart, TimeFormat, CultureInfo.InvariantCulture),
        DayEnd = TimeOnly.ParseExact(v.DayEnd, TimeFormat, CultureInfo.InvariantCulture),
        Shortcuts = v.Shortcuts.Select(s => new ShortcutLink { Title = s.Title, Url = s.Url }).ToList(),
        ShowDayPreviewOnFocusStart = v.ShowDayPreviewOnFocusStart,
        GranularityMinutes = ScheduleGeometry.NormalizeGranularity(v.GranularityMinutes),
        DefaultPreAlertMinutes = Math.Clamp(v.DefaultPreAlertMinutes, 0, 1440)
    };
}
