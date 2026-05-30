using System;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Interop;

namespace Ritmo.Core.Tests;

public class CalendarEventsTests
{
    private const string Ics =
        "BEGIN:VCALENDAR\r\n" +
        "BEGIN:VEVENT\r\nSUMMARY:Dentista\r\nDTSTART:20240103T100000\r\nDTEND:20240103T110000\r\nEND:VEVENT\r\n" +
        "BEGIN:VEVENT\r\nSUMMARY:Reunion semanal\r\nDTSTART:20240101T090000\r\nDTEND:20240101T093000\r\nRRULE:FREQ=WEEKLY;BYDAY=MO\r\nEND:VEVENT\r\n" +
        "BEGIN:VEVENT\r\nSUMMARY:Festivo\r\nDTSTART;VALUE=DATE:20240105\r\nEND:VEVENT\r\n" +
        "END:VCALENDAR\r\n";

    [Fact]
    public void ImportEvents_una_semana()
    {
        var ev = ICalendar.ImportEvents(Ics, new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 7));
        // Dentista (mié) + Reunión (lun, 1 ocurrencia) + Festivo (todo el día) = 3.
        Assert.Equal(3, ev.Count);
        Assert.Contains(ev, e => e.Title == "Dentista" && e.Start == new DateTime(2024, 1, 3, 10, 0, 0));
        var festivo = ev.Single(e => e.Title == "Festivo");
        Assert.True(festivo.AllDay);
        var reunion = ev.Single(e => e.Title == "Reunion semanal");
        Assert.Equal(DayOfWeek.Monday, reunion.Start.DayOfWeek);
    }

    [Fact]
    public void ImportEvents_recurrencia_semanal_se_expande_en_el_rango()
    {
        // Tres semanas → tres lunes.
        var ev = ICalendar.ImportEvents(Ics, new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 21));
        Assert.Equal(3, ev.Count(e => e.Title == "Reunion semanal"));
    }

    [Fact]
    public void ImportEvents_respeta_UNTIL()
    {
        var ics =
            "BEGIN:VEVENT\r\nSUMMARY:Clase\r\nDTSTART:20240101T090000\r\nDTEND:20240101T100000\r\n" +
            "RRULE:FREQ=WEEKLY;BYDAY=MO;UNTIL=20240108T235959\r\nEND:VEVENT\r\n";
        var ev = ICalendar.ImportEvents(ics, new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31));
        Assert.Equal(2, ev.Count);   // 1 ene y 8 ene; el 15 ya pasa UNTIL.
    }

    [Fact]
    public void ImportEvents_fuera_de_rango_o_vacio()
    {
        // Un evento puntual de 2024 no aparece al consultar 2025.
        var oneOff = "BEGIN:VEVENT\r\nSUMMARY:Cita\r\nDTSTART:20240103T100000\r\nDTEND:20240103T110000\r\nEND:VEVENT\r\n";
        Assert.Empty(ICalendar.ImportEvents(oneOff, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 7)));
        Assert.Empty(ICalendar.ImportEvents("", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 7)));
    }

    [Fact]
    public void Feed_CRUD()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);

        var r = svc.AddCalendarFeed("Trabajo", "https://example.com/cal.ics");
        Assert.True(r.Success);
        var feed = Assert.Single(store.Load().CalendarFeeds);
        Assert.Equal("Trabajo", feed.Name);

        Assert.False(svc.AddCalendarFeed("X", "no-es-url").Success);

        Assert.True(svc.RemoveCalendarFeed(feed.Id).Success);
        Assert.Empty(store.Load().CalendarFeeds);
    }
}
