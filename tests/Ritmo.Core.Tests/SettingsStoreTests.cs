using Ritmo.Core.Model;
using Ritmo.Core.Persistence;
using Ritmo.Core.Pomodoro;

namespace Ritmo.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public SettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "RitmoTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* limpieza best-effort */ }
    }

    private static AppSettings Sample() => new()
    {
        Schedule = new WeeklySchedule
        {
            Sessions =
            [
                new StudySession
                {
                    Title = "Técnico ▸ siguiente tema",
                    Day = DayOfWeek.Monday,
                    Start = new TimeOnly(9, 0),
                    Duration = TimeSpan.FromHours(2),
                    Kind = StudyKind.Tecnico,
                    PreAlerts = [PreAlert.OneHour, PreAlert.TenMinutes]
                },
                new StudySession
                {
                    Title = "Inglés",
                    Day = DayOfWeek.Thursday,
                    Start = new TimeOnly(9, 0),
                    Duration = TimeSpan.FromHours(2),
                    Kind = StudyKind.Ingles
                }
            ]
        },
        Pomodoro = PomodoroConfig.DeepWork
    };

    [Fact]
    public void Archivo_inexistente_devuelve_Default()
    {
        var store = new JsonSettingsStore(_file);
        var loaded = store.Load();
        Assert.Empty(loaded.Schedule.Sessions);
        Assert.Equal(PomodoroConfig.DeepWork.Focus, loaded.Pomodoro.Focus);
    }

    [Fact]
    public void RoundTrip_conserva_todo()
    {
        var store = new JsonSettingsStore(_file);
        store.Save(Sample());
        var loaded = store.Load();

        Assert.Equal(2, loaded.Schedule.Sessions.Count);

        var tec = loaded.Schedule.Sessions[0];
        Assert.Equal("Técnico ▸ siguiente tema", tec.Title);
        Assert.Equal(DayOfWeek.Monday, tec.Day);
        Assert.Equal(new TimeOnly(9, 0), tec.Start);
        Assert.Equal(TimeSpan.FromHours(2), tec.Duration);
        Assert.Equal(StudyKind.Tecnico, tec.Kind);
        Assert.Equal(new[] { 60, 10 }, tec.PreAlerts.Select(a => a.MinutesBefore).ToArray());

        Assert.Equal(DayOfWeek.Thursday, loaded.Schedule.Sessions[1].Day);

        Assert.Equal(TimeSpan.FromMinutes(50), loaded.Pomodoro.Focus);
        Assert.Equal(2, loaded.Pomodoro.FocusesPerLongBreak);
    }

    [Fact]
    public void El_archivo_creado_es_JSON_legible()
    {
        var store = new JsonSettingsStore(_file);
        store.Save(Sample());

        Assert.True(File.Exists(_file));
        var json = File.ReadAllText(_file);
        // Horas legibles y duraciones en minutos, no tipos crudos.
        Assert.Contains("\"09:00\"", json);
        Assert.Contains("Monday", json);
        Assert.Contains("focusMinutes", json);     // camelCase
    }

    [Fact]
    public void Save_sobreescribe_el_contenido_anterior()
    {
        var store = new JsonSettingsStore(_file);
        store.Save(Sample());

        var menos = new AppSettings
        {
            Schedule = new WeeklySchedule { Sessions = [] },
            Pomodoro = PomodoroConfig.Classic
        };
        store.Save(menos);

        var loaded = store.Load();
        Assert.Empty(loaded.Schedule.Sessions);
        Assert.Equal(TimeSpan.FromMinutes(25), loaded.Pomodoro.Focus);
    }

    [Fact]
    public void JSON_corrupto_devuelve_Default_sin_lanzar()
    {
        File.WriteAllText(_file, "{ esto no es json válido ");
        var store = new JsonSettingsStore(_file);
        var loaded = store.Load();
        Assert.Empty(loaded.Schedule.Sessions); // Default, sin excepción
    }

    [Fact]
    public void Save_crea_el_directorio_si_no_existe()
    {
        var nested = Path.Combine(_dir, "sub", "carpeta", "settings.json");
        var store = new JsonSettingsStore(nested);
        store.Save(Sample());
        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void Default_apunta_a_LocalAppData_Ritmo()
    {
        var store = JsonSettingsStore.Default();
        Assert.Contains("Ritmo", store.FilePath);
        Assert.EndsWith("settings.json", store.FilePath);
    }
}
