using System;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

/// <summary>Auto-cómputo de horas desde sesiones vinculadas + excepciones (#137).</summary>
public class WorkAutoComputeTests
{
    private static StudySession Sess(string title, DayOfWeek day, int startH, double hours, string? projectId)
        => new()
        {
            Title = title, Day = day, Start = new TimeOnly(startH, 0),
            Duration = TimeSpan.FromHours(hours), CategoryId = "Otro", ProjectId = projectId
        };

    [Fact]
    public void Suma_horas_de_las_sesiones_vinculadas_los_dias_que_tocan()
    {
        // Agosto 2026: los lunes son 3, 10, 17, 24, 31 → 5 lunes. Sesión de 4 h vinculada a p1.
        var schedule = new[] { Sess("Heladería", DayOfWeek.Monday, 16, 4, "p1") };
        double total = WorkAutoCompute.MonthAutoHours(schedule, [], "p1", 2026, 8);
        Assert.Equal(20, total);   // 5 lunes × 4 h
    }

    [Fact]
    public void Ignora_sesiones_de_otro_proyecto_o_sin_vincular()
    {
        var schedule = new[]
        {
            Sess("A", DayOfWeek.Monday, 9, 3, "p1"),
            Sess("B", DayOfWeek.Monday, 13, 2, "p2"),
            Sess("C", DayOfWeek.Monday, 17, 1, null),
        };
        // Solo la de p1 cuenta para p1.
        Assert.Equal(15, WorkAutoCompute.MonthAutoHours(schedule, [], "p1", 2026, 8));   // 5 lunes × 3
    }

    [Fact]
    public void Una_excepcion_de_un_dia_no_computa_ese_dia()
    {
        var schedule = new[] { Sess("Heladería", DayOfWeek.Monday, 16, 4, "p1") };
        var key = SessionKey.For(schedule[0]);
        var exc = new[] { new SessionException { Id = "e1", SessionKey = key, From = new DateOnly(2026, 8, 10), To = new DateOnly(2026, 8, 10) } };
        // 5 lunes − 1 cancelado = 4 × 4 h.
        Assert.Equal(16, WorkAutoCompute.MonthAutoHours(schedule, exc, "p1", 2026, 8));
        Assert.True(WorkAutoCompute.IsCancelled(schedule[0], new DateOnly(2026, 8, 10), exc));
        Assert.False(WorkAutoCompute.IsCancelled(schedule[0], new DateOnly(2026, 8, 3), exc));
    }

    [Fact]
    public void Una_excepcion_de_rango_cancela_varios_dias()
    {
        var schedule = new[] { Sess("Heladería", DayOfWeek.Monday, 16, 4, "p1") };
        var key = SessionKey.For(schedule[0]);
        // Rango que cubre los lunes 10 y 17.
        var exc = new[] { new SessionException { Id = "e1", SessionKey = key, From = new DateOnly(2026, 8, 8), To = new DateOnly(2026, 8, 20) } };
        Assert.Equal(12, WorkAutoCompute.MonthAutoHours(schedule, exc, "p1", 2026, 8));   // 5 − 2 = 3 × 4
    }

    [Fact]
    public void DailyAutoHours_coloca_las_horas_en_el_dia_correcto()
    {
        var schedule = new[] { Sess("Heladería", DayOfWeek.Monday, 16, 4, "p1") };
        var daily = WorkAutoCompute.DailyAutoHours(schedule, [], "p1", 2026, 8);
        Assert.Equal(4, daily[2]);    // día 3 (lunes)
        Assert.Equal(0, daily[3]);    // día 4 (martes)
        Assert.Equal(4, daily[9]);    // día 10 (lunes)
    }

    // ---- Comandos de fachada ----

    private static ConfigurationService NewWithProjectAndSession(out InMemorySettingsStore store, out string projId, out string sessionKey)
    {
        store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        projId = svc.AddWorkProject("Heladería", 6m).Message;
        svc.AddPhase("Fase 1", new DateOnly(2026, 1, 1), null);
        var sess = new StudySession { Title = "Turno", Day = DayOfWeek.Monday, Start = new TimeOnly(16, 0), Duration = TimeSpan.FromHours(4), CategoryId = "Otro" };
        svc.AddSession("Fase 1", sess);
        sessionKey = SessionKey.For(sess);
        return svc;
    }

    [Fact]
    public void SetSessionProject_vincula_y_desvincula()
    {
        var svc = NewWithProjectAndSession(out var store, out var projId, out var key);
        Assert.True(svc.SetSessionProject(key, projId).Success);
        Assert.Equal(projId, store.Load().Plan.Phases.Single().Schedule.Sessions.Single().ProjectId);
        Assert.True(svc.SetSessionProject(key, null).Success);
        Assert.Null(store.Load().Plan.Phases.Single().Schedule.Sessions.Single().ProjectId);
    }

    [Fact]
    public void SetSessionProject_rechaza_proyecto_inexistente()
    {
        var svc = NewWithProjectAndSession(out _, out _, out var key);
        Assert.False(svc.SetSessionProject(key, "noexiste").Success);
    }

    [Fact]
    public void AddSessionException_y_Remove()
    {
        var svc = NewWithProjectAndSession(out var store, out _, out var key);
        Assert.True(svc.AddSessionException(key, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 10), "fiesta").Success);
        var ex = store.Load().SessionExceptions.Single();
        Assert.Equal(key, ex.SessionKey);
        Assert.True(svc.RemoveSessionException(ex.Id).Success);
        Assert.Empty(store.Load().SessionExceptions);
    }

    [Fact]
    public void AddSessionException_rechaza_rango_invertido()
    {
        var svc = NewWithProjectAndSession(out _, out _, out var key);
        Assert.False(svc.AddSessionException(key, new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 1)).Success);
    }

    [Fact]
    public void Vinculo_y_excepciones_sobreviven_al_round_trip()
    {
        var svc = NewWithProjectAndSession(out var store, out var projId, out var key);
        svc.SetSessionProject(key, projId);
        svc.AddSessionException(key, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12), "baja");
        var json = svc.ExportJson();

        var store2 = new InMemorySettingsStore();
        var svc2 = new ConfigurationService(store2);
        Assert.True(svc2.ImportJson(json).Success);
        var s = store2.Load();
        Assert.Equal(projId, s.Plan.Phases.Single().Schedule.Sessions.Single().ProjectId);
        var ex = s.SessionExceptions.Single();
        Assert.Equal(new DateOnly(2026, 8, 10), ex.From);
        Assert.Equal(new DateOnly(2026, 8, 12), ex.To);
        Assert.Equal("baja", ex.Reason);
    }
}
