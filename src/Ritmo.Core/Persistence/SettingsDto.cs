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
    // Nuevo: horario por fases, notas y configuración de vista.
    public List<PhaseDto> Phases { get; set; } = [];
    public List<NoteDto> Notes { get; set; } = [];
    public ViewConfigDto? ViewConfig { get; set; }
    public List<FocusEnvironmentDto> FocusEnvironments { get; set; } = [];
    public string? DefaultFocusEnvironmentId { get; set; }
    public Dictionary<string, string> EnvironmentByKind { get; set; } = [];
}

internal sealed class MusicDto
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Arguments { get; set; }
    public bool AutoPlay { get; set; }
}

internal sealed class FocusEnvironmentDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? PomodoroPreset { get; set; }
    public bool EnableDoNotDisturb { get; set; } = true;
    public bool HideTaskbarBadges { get; set; } = true;
    public bool ShowDayPreview { get; set; } = true;
    public bool OpenStudyListInEdge { get; set; }
    public List<string> BlockedWebsites { get; set; } = [];
    public List<string> AppsToClose { get; set; } = [];
    public List<string> AppsToMute { get; set; } = [];
    public MusicDto? Music { get; set; }
    public List<ShortcutDto> Links { get; set; } = [];
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
}

internal sealed class PomodoroDto
{
    public int FocusMinutes { get; set; } = 50;
    public int ShortBreakMinutes { get; set; } = 10;
    public int LongBreakMinutes { get; set; } = 20;
    public int FocusesPerLongBreak { get; set; } = 2;
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
        Phases = s.Plan.Phases.Select(ToDto).ToList(),
        Notes = s.Notes.Select(ToDto).ToList(),
        ViewConfig = ToDto(s.ViewConfig),
        FocusEnvironments = s.FocusEnvironments.Select(ToDto).ToList(),
        DefaultFocusEnvironmentId = s.DefaultFocusEnvironmentId,
        EnvironmentByKind = s.EnvironmentByKind.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
    };

    private static FocusEnvironmentDto ToDto(FocusEnvironment e) => new()
    {
        Id = e.Id, Name = e.Name, PomodoroPreset = e.PomodoroPreset,
        EnableDoNotDisturb = e.EnableDoNotDisturb, HideTaskbarBadges = e.HideTaskbarBadges,
        ShowDayPreview = e.ShowDayPreview, OpenStudyListInEdge = e.OpenStudyListInEdge,
        BlockedWebsites = e.BlockedWebsites.ToList(),
        AppsToClose = e.AppsToClose.ToList(),
        AppsToMute = e.AppsToMute.ToList(),
        Music = e.Music is null ? null : new MusicDto
        {
            Name = e.Music.Name, Target = e.Music.Target,
            Arguments = e.Music.Arguments, AutoPlay = e.Music.AutoPlay
        },
        Links = e.Links.Select(l => new ShortcutDto { Title = l.Title, Url = l.Url }).ToList()
    };

    private static SessionDto ToDto(StudySession x) => new()
    {
        Title = x.Title,
        Day = x.Day.ToString(),
        Start = x.Start.ToString(TimeFormat, CultureInfo.InvariantCulture),
        DurationMinutes = (int)x.Duration.TotalMinutes,
        Kind = x.Kind.ToString(),
        PreAlertsMinutes = x.PreAlerts.Select(a => a.MinutesBefore).ToList(),
        IsTentative = x.IsTentative
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
        AccentColor = n.AccentColor, Order = n.Order
    };

    private static ViewConfigDto ToDto(ScheduleViewConfig v) => new()
    {
        DayStart = v.DayStart.ToString(TimeFormat, CultureInfo.InvariantCulture),
        DayEnd = v.DayEnd.ToString(TimeFormat, CultureInfo.InvariantCulture),
        ColorsByKind = v.ColorsByKind.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
        Shortcuts = v.Shortcuts.Select(s => new ShortcutDto { Title = s.Title, Url = s.Url }).ToList(),
        ShowDayPreviewOnFocusStart = v.ShowDayPreviewOnFocusStart
    };

    // ---------- DTO -> Dominio ----------
    public static AppSettings FromDto(SettingsDto d) => new()
    {
        Schedule = new WeeklySchedule { Sessions = d.Sessions.Select(FromDto).ToList() },
        Pomodoro = new PomodoroConfig(
            TimeSpan.FromMinutes(d.Pomodoro.FocusMinutes),
            TimeSpan.FromMinutes(d.Pomodoro.ShortBreakMinutes),
            TimeSpan.FromMinutes(d.Pomodoro.LongBreakMinutes),
            d.Pomodoro.FocusesPerLongBreak),
        Plan = new SchedulePlan { Phases = d.Phases.Select(FromDto).ToList() },
        Notes = d.Notes.Select(FromDto).ToList(),
        ViewConfig = d.ViewConfig is null ? new ScheduleViewConfig() : FromDto(d.ViewConfig),
        FocusEnvironments = d.FocusEnvironments.Select(FromDto).ToList(),
        DefaultFocusEnvironmentId = d.DefaultFocusEnvironmentId,
        EnvironmentByKind = d.EnvironmentByKind
            .Where(kv => Enum.TryParse<StudyKind>(kv.Key, ignoreCase: true, out _))
            .ToDictionary(kv => Enum.Parse<StudyKind>(kv.Key, ignoreCase: true), kv => kv.Value)
    };

    private static FocusEnvironment FromDto(FocusEnvironmentDto e) => new()
    {
        Id = e.Id, Name = e.Name, PomodoroPreset = e.PomodoroPreset,
        EnableDoNotDisturb = e.EnableDoNotDisturb, HideTaskbarBadges = e.HideTaskbarBadges,
        ShowDayPreview = e.ShowDayPreview, OpenStudyListInEdge = e.OpenStudyListInEdge,
        BlockedWebsites = e.BlockedWebsites.ToList(),
        AppsToClose = e.AppsToClose.ToList(),
        AppsToMute = e.AppsToMute.ToList(),
        Music = e.Music is null ? null : new MusicLauncher
        {
            Name = e.Music.Name, Target = e.Music.Target,
            Arguments = e.Music.Arguments, AutoPlay = e.Music.AutoPlay
        },
        Links = e.Links.Select(l => new ShortcutLink { Title = l.Title, Url = l.Url }).ToList()
    };

    private static StudySession FromDto(SessionDto x) => new()
    {
        Title = x.Title,
        Day = Enum.Parse<DayOfWeek>(x.Day, ignoreCase: true),
        Start = TimeOnly.ParseExact(x.Start, TimeFormat, CultureInfo.InvariantCulture),
        Duration = TimeSpan.FromMinutes(x.DurationMinutes),
        Kind = Enum.TryParse<StudyKind>(x.Kind, ignoreCase: true, out var k) ? k : StudyKind.Otro,
        PreAlerts = x.PreAlertsMinutes.Select(m => new PreAlert(m)).ToList(),
        IsTentative = x.IsTentative
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
        AccentColor = n.AccentColor, Order = n.Order
    };

    private static ScheduleViewConfig FromDto(ViewConfigDto v) => new()
    {
        DayStart = TimeOnly.ParseExact(v.DayStart, TimeFormat, CultureInfo.InvariantCulture),
        DayEnd = TimeOnly.ParseExact(v.DayEnd, TimeFormat, CultureInfo.InvariantCulture),
        ColorsByKind = v.ColorsByKind
            .Where(kv => Enum.TryParse<StudyKind>(kv.Key, ignoreCase: true, out _))
            .ToDictionary(kv => Enum.Parse<StudyKind>(kv.Key, ignoreCase: true), kv => kv.Value),
        Shortcuts = v.Shortcuts.Select(s => new ShortcutLink { Title = s.Title, Url = s.Url }).ToList(),
        ShowDayPreviewOnFocusStart = v.ShowDayPreviewOnFocusStart
    };
}
