using Ritmo.Core.Model;
using Ritmo.Core.Persistence;
using Ritmo.Core.Pomodoro;

namespace Ritmo.Core.Tests;

public class SettingsStoreAdvancedTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public SettingsStoreAdvancedTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "RitmoAdv_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    private static AppSettings Full() => new()
    {
        Plan = new SchedulePlan
        {
            Phases =
            [
                new SchedulePhase
                {
                    Name = "Fase 1",
                    ValidFrom = new DateOnly(2026, 6, 1),
                    ValidTo = new DateOnly(2026, 10, 31),
                    Schedule = new WeeklySchedule
                    {
                        Sessions =
                        [
                            new StudySession {
                                Title = "Técnico", Day = DayOfWeek.Monday,
                                Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(2),
                                Kind = StudyKind.Tecnico, PreAlerts = [PreAlert.TenMinutes] },
                            new StudySession {
                                Title = "Hueco libre", Day = DayOfWeek.Tuesday,
                                Start = new TimeOnly(16,0), Duration = TimeSpan.FromHours(2),
                                Kind = StudyKind.PorDefinir, IsTentative = true }
                        ]
                    }
                },
                new SchedulePhase
                {
                    Name = "Fase 2",
                    ValidFrom = new DateOnly(2026, 11, 1),
                    ValidTo = null, // indefinida
                    Schedule = new WeeklySchedule()
                }
            ]
        },
        Notes =
        [
            new StudyNote { Id = "ojo", Title = "¡OJO!", Content = "El **jurídico** primero", AccentColor = "#C0392B", Order = 0 },
            new StudyNote { Id = "rec", Title = "Recursos", Content = "Campus ZBrain", Order = 1 }
        ],
        ViewConfig = new ScheduleViewConfig
        {
            DayStart = new TimeOnly(8, 0),
            DayEnd = new TimeOnly(20, 0),
            ColorsByKind = new Dictionary<StudyKind, string> { [StudyKind.Tecnico] = "#E2EFDA" },
            Shortcuts = [ new ShortcutLink { Title = "Campus", Url = "https://campus.zbrain.es" } ],
            ShowDayPreviewOnFocusStart = false
        },
        Pomodoro = PomodoroConfig.DeepWork
    };

    [Fact]
    public void RoundTrip_del_plan_completo()
    {
        var store = new JsonSettingsStore(_file);
        store.Save(Full());
        var loaded = store.Load();

        // Fases
        Assert.Equal(2, loaded.Plan.Phases.Count);
        var f1 = loaded.Plan.OrderedPhases[0];
        Assert.Equal("Fase 1", f1.Name);
        Assert.Equal(new DateOnly(2026, 6, 1), f1.ValidFrom);
        Assert.Equal(new DateOnly(2026, 10, 31), f1.ValidTo);
        Assert.Equal(2, f1.Schedule.Sessions.Count);
        // El bloque tentativo "Por definir" se conserva.
        var hueco = f1.Schedule.Sessions.Single(s => s.Title == "Hueco libre");
        Assert.True(hueco.IsTentative);
        Assert.Equal(StudyKind.PorDefinir, hueco.Kind);
        // Fase indefinida
        Assert.Null(loaded.Plan.OrderedPhases[1].ValidTo);
    }

    [Fact]
    public void RoundTrip_de_notas_y_viewconfig()
    {
        var store = new JsonSettingsStore(_file);
        store.Save(Full());
        var loaded = store.Load();

        Assert.Equal(2, loaded.Notes.Count);
        Assert.Equal("¡OJO!", loaded.Notes.OrderBy(n => n.Order).First().Title);
        Assert.Equal("#C0392B", loaded.Notes.First(n => n.Id == "ojo").AccentColor);

        Assert.Equal(new TimeOnly(8, 0), loaded.ViewConfig.DayStart);
        Assert.Equal("#E2EFDA", loaded.ViewConfig.ColorFor(StudyKind.Tecnico));
        Assert.Single(loaded.ViewConfig.Shortcuts);
        Assert.False(loaded.ViewConfig.ShowDayPreviewOnFocusStart);
    }

    [Fact]
    public void Plan_activo_funciona_tras_recargar()
    {
        var store = new JsonSettingsStore(_file);
        store.Save(Full());
        var loaded = store.Load();
        // En agosto rige Fase 1.
        Assert.Equal("Fase 1", loaded.Plan.GetActivePhase(new DateOnly(2026, 8, 1))!.Name);
        // En diciembre, Fase 2.
        Assert.Equal("Fase 2", loaded.Plan.GetActivePhase(new DateOnly(2026, 12, 1))!.Name);
    }

    [Fact]
    public void JSON_antiguo_solo_sessions_sigue_cargando()
    {
        // Simula un settings.json del formato original (#19), sin phases/notes/viewConfig.
        var legacy = """
        {
          "sessions": [
            { "title": "Legacy", "day": "Monday", "start": "09:00", "durationMinutes": 60, "kind": "Tecnico", "preAlertsMinutes": [10] }
          ],
          "pomodoro": { "focusMinutes": 25, "shortBreakMinutes": 5, "longBreakMinutes": 15, "focusesPerLongBreak": 4 }
        }
        """;
        File.WriteAllText(_file, legacy);

        var loaded = new JsonSettingsStore(_file).Load();
        // El horario suelto se carga.
        Assert.Single(loaded.Schedule.Sessions);
        Assert.Equal("Legacy", loaded.Schedule.Sessions[0].Title);
        // Lo nuevo cae a valores por defecto sin reventar.
        Assert.Empty(loaded.Plan.Phases);
        Assert.Empty(loaded.Notes);
        Assert.NotNull(loaded.ViewConfig);
        Assert.Equal(new TimeOnly(8, 0), loaded.ViewConfig.DayStart);
        Assert.Equal(TimeSpan.FromMinutes(25), loaded.Pomodoro.Focus);
    }
}
