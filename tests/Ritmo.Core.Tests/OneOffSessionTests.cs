using System;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

public class OneOffSessionTests
{
    [Fact]
    public void AsSession_deriva_el_dia_de_la_fecha()
    {
        // 2026-05-28 es jueves.
        var one = new OneOffSession
        {
            Id = "x", Date = new DateOnly(2026, 5, 28), Title = "Clase extra",
            Start = new TimeOnly(15, 0), Duration = TimeSpan.FromHours(1), Kind = StudyKind.Ingles
        };
        var s = one.AsSession();
        Assert.Equal(DayOfWeek.Thursday, s.Day);
        Assert.Equal("Clase extra", s.Title);
        Assert.Equal(StudyKind.Ingles, s.Kind);
    }

    [Fact]
    public void Add_y_Remove_provisional()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);

        var r = svc.AddOneOffSession(new DateOnly(2026, 5, 28), "Clase extra",
            new TimeOnly(15, 0), TimeSpan.FromHours(1), StudyKind.Ingles, [new PreAlert(10)], false);
        Assert.True(r.Success);
        var one = Assert.Single(store.Load().OneOffSessions);
        Assert.Equal("Clase extra", one.Title);
        Assert.Equal(new DateOnly(2026, 5, 28), one.Date);

        Assert.False(svc.AddOneOffSession(new DateOnly(2026, 5, 28), "  ",
            new TimeOnly(15, 0), TimeSpan.FromHours(1), StudyKind.Otro, [], false).Success);   // sin título

        Assert.True(svc.RemoveOneOffSession(one.Id).Success);
        Assert.Empty(store.Load().OneOffSessions);
    }

    [Fact]
    public void Sobrevive_export_import()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.AddOneOffSession(new DateOnly(2026, 5, 28), "Clase extra",
            new TimeOnly(15, 30), TimeSpan.FromMinutes(90), StudyKind.Tecnico, [], true);

        var json = svc.ExportJson();
        var other = new ConfigurationService(new InMemorySettingsStore());
        Assert.True(other.ImportJson(json).Success);

        var one = Assert.Single(other.GetSettings().OneOffSessions);
        Assert.Equal(new DateOnly(2026, 5, 28), one.Date);
        Assert.Equal(new TimeOnly(15, 30), one.Start);
        Assert.Equal(TimeSpan.FromMinutes(90), one.Duration);
        Assert.True(one.IsTentative);
    }
}
