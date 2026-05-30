using System;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Interop;
using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

public class OverlapResolverTests
{
    private static StudySession Session(DayOfWeek day, int hour, int durHours = 2) => new()
    {
        Title = "Legislación", Day = day,
        Start = new TimeOnly(hour, 0), Duration = TimeSpan.FromHours(durHours),
        Kind = StudyKind.Legislacion
    };

    private static CalendarEvent Event(DateTime start, double durHours, bool allDay = false, string? cal = "Trabajo")
        => new("Reunión", start, start.AddHours(durHours), allDay, cal);

    // 2024-01-01 es lunes.
    private static readonly DateOnly Monday = new(2024, 1, 1);

    [Fact]
    public void Overlaps_evento_temporal_que_pisa_la_sesion()
    {
        var s = Session(DayOfWeek.Monday, 9);                       // 09:00–11:00
        var ev = Event(new DateTime(2024, 1, 1, 10, 0, 0), 1);      // 10:00–11:00
        Assert.True(OverlapResolver.Overlaps(s, Monday, ev));
    }

    [Fact]
    public void NoOverlaps_evento_de_todo_el_dia()
    {
        var s = Session(DayOfWeek.Monday, 9);
        var ev = Event(new DateTime(2024, 1, 1, 0, 0, 0), 24, allDay: true);
        Assert.False(OverlapResolver.Overlaps(s, Monday, ev));
    }

    [Fact]
    public void NoOverlaps_distinto_dia_o_franja_que_solo_toca()
    {
        var s = Session(DayOfWeek.Monday, 9);                       // 09:00–11:00
        var otroDia = Event(new DateTime(2024, 1, 2, 10, 0, 0), 1); // martes
        Assert.False(OverlapResolver.Overlaps(s, Monday, otroDia));

        var pegado = Event(new DateTime(2024, 1, 1, 11, 0, 0), 1);  // 11:00–12:00, solo toca el borde
        Assert.False(OverlapResolver.Overlaps(s, Monday, pegado));
    }

    [Fact]
    public void EventsOverlapping_y_SessionsOverlapping_son_coherentes()
    {
        var s = Session(DayOfWeek.Monday, 9);
        var pisa = Event(new DateTime(2024, 1, 1, 9, 30, 0), 1);
        var noPisa = Event(new DateTime(2024, 1, 1, 12, 0, 0), 1);

        var hits = OverlapResolver.EventsOverlapping(s, Monday, [pisa, noPisa]);
        Assert.Single(hits);
        Assert.Same(pisa, hits[0]);

        var sess = OverlapResolver.SessionsOverlapping(pisa, [s]);
        Assert.Single(sess);
        Assert.Empty(OverlapResolver.SessionsOverlapping(noPisa, [s]));
    }

    [Fact]
    public void EventKey_es_estable_y_distingue_eventos()
    {
        var a = Event(new DateTime(2024, 1, 1, 9, 0, 0), 1);
        var aOtraVez = Event(new DateTime(2024, 1, 1, 9, 0, 0), 1);
        var distinto = Event(new DateTime(2024, 1, 1, 10, 0, 0), 1);

        Assert.Equal(OverlapResolver.EventKey(a), OverlapResolver.EventKey(aOtraVez));
        Assert.NotEqual(OverlapResolver.EventKey(a), OverlapResolver.EventKey(distinto));
    }

    [Fact]
    public void Prioridad_se_guarda_reemplaza_y_se_limpia()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        const string key = "Trabajo|Reunión|2024-01-01T09:00:00";

        Assert.True(svc.SetOverlapPriority(key, preferCalendar: true).Success);
        var p = Assert.Single(store.Load().OverlapPriorities);
        Assert.True(p.PreferCalendar);

        // Reemplaza (no duplica) la decisión del mismo evento.
        Assert.True(svc.SetOverlapPriority(key, preferCalendar: false).Success);
        p = Assert.Single(store.Load().OverlapPriorities);
        Assert.False(p.PreferCalendar);

        Assert.True(svc.ClearOverlapPriority(key).Success);
        Assert.Empty(store.Load().OverlapPriorities);
    }

    [Fact]
    public void Prioridad_sobrevive_export_import()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.SetOverlapPriority("k|t|2024-01-01T09:00:00", preferCalendar: true);

        var json = svc.ExportJson();
        var other = new ConfigurationService(new InMemorySettingsStore());
        Assert.True(other.ImportJson(json).Success);
        Assert.Single(other.GetSettings().OverlapPriorities);
    }
}
