using System.Globalization;
using Ritmo.Core.Model;
using Ritmo.Core.Pomodoro;

namespace Ritmo.Core.Persistence;

// DTOs de almacenamiento: representación JSON-friendly y editable a mano.
// Las horas van como "HH:mm" y las duraciones en minutos (números), no como
// los tipos TimeOnly/TimeSpan (que serializan de forma fea o ambigua).

internal sealed class SettingsDto
{
    public List<SessionDto> Sessions { get; set; } = [];
    public PomodoroDto Pomodoro { get; set; } = new();
}

internal sealed class SessionDto
{
    public string Title { get; set; } = "";
    public string Day { get; set; } = "Monday";
    public string Start { get; set; } = "09:00";
    public int DurationMinutes { get; set; } = 60;
    public string Kind { get; set; } = "Otro";
    public List<int> PreAlertsMinutes { get; set; } = [];
}

internal sealed class PomodoroDto
{
    public int FocusMinutes { get; set; } = 50;
    public int ShortBreakMinutes { get; set; } = 10;
    public int LongBreakMinutes { get; set; } = 20;
    public int FocusesPerLongBreak { get; set; } = 2;
}

/// <summary>Conversión entre el modelo de dominio y los DTO de almacenamiento.</summary>
internal static class SettingsMapper
{
    private const string TimeFormat = "HH\\:mm";

    public static SettingsDto ToDto(AppSettings s) => new()
    {
        Sessions = s.Schedule.Sessions.Select(ToDto).ToList(),
        Pomodoro = new PomodoroDto
        {
            FocusMinutes = (int)s.Pomodoro.Focus.TotalMinutes,
            ShortBreakMinutes = (int)s.Pomodoro.ShortBreak.TotalMinutes,
            LongBreakMinutes = (int)s.Pomodoro.LongBreak.TotalMinutes,
            FocusesPerLongBreak = s.Pomodoro.FocusesPerLongBreak
        }
    };

    private static SessionDto ToDto(StudySession x) => new()
    {
        Title = x.Title,
        Day = x.Day.ToString(),
        Start = x.Start.ToString(TimeFormat, CultureInfo.InvariantCulture),
        DurationMinutes = (int)x.Duration.TotalMinutes,
        Kind = x.Kind.ToString(),
        PreAlertsMinutes = x.PreAlerts.Select(a => a.MinutesBefore).ToList()
    };

    public static AppSettings FromDto(SettingsDto d) => new()
    {
        Schedule = new WeeklySchedule
        {
            Sessions = d.Sessions.Select(FromDto).ToList()
        },
        Pomodoro = new PomodoroConfig(
            TimeSpan.FromMinutes(d.Pomodoro.FocusMinutes),
            TimeSpan.FromMinutes(d.Pomodoro.ShortBreakMinutes),
            TimeSpan.FromMinutes(d.Pomodoro.LongBreakMinutes),
            d.Pomodoro.FocusesPerLongBreak)
    };

    private static StudySession FromDto(SessionDto x) => new()
    {
        Title = x.Title,
        Day = Enum.Parse<DayOfWeek>(x.Day, ignoreCase: true),
        Start = TimeOnly.ParseExact(x.Start, TimeFormat, CultureInfo.InvariantCulture),
        Duration = TimeSpan.FromMinutes(x.DurationMinutes),
        Kind = Enum.TryParse<StudyKind>(x.Kind, ignoreCase: true, out var k) ? k : StudyKind.Otro,
        PreAlerts = x.PreAlertsMinutes.Select(m => new PreAlert(m)).ToList()
    };
}
