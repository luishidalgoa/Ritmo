using Ritmo.Core.Sync;

namespace Ritmo.Core.Tests;

public class IcalTodoTests
{
    [Fact]
    public void Build_incluye_uid_summary_y_estado()
    {
        var ics = IcalTodo.Build("uid-1", "Comprar pan", done: false, "20260602T100000Z");
        Assert.Contains("BEGIN:VTODO", ics);
        Assert.Contains("UID:uid-1", ics);
        Assert.Contains("SUMMARY:Comprar pan", ics);
        Assert.Contains("STATUS:NEEDS-ACTION", ics);

        var done = IcalTodo.Build("uid-2", "Hecho", done: true, "20260602T100000Z");
        Assert.Contains("STATUS:COMPLETED", done);
        Assert.Contains("PERCENT-COMPLETE:100", done);
    }

    [Fact]
    public void Escape_y_unescape_son_inversos()
    {
        var s = "Comprar pan, leche; y café\ncon nota";
        var roundtrip = IcalTodo.Unescape(IcalTodo.Escape(s));
        Assert.Equal(s.Replace("\r\n", "\n"), roundtrip);
    }

    [Fact]
    public void Parse_lee_el_vtodo()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VTODO\r\nUID:abc\r\n" +
                  "SUMMARY:Tarea de prueba\r\nSTATUS:COMPLETED\r\nLAST-MODIFIED:20260601T090000Z\r\n" +
                  "END:VTODO\r\nEND:VCALENDAR\r\n";
        var t = IcalTodo.Parse(ics);
        Assert.NotNull(t);
        Assert.Equal("abc", t!.Value.Uid);
        Assert.Equal("Tarea de prueba", t.Value.Summary);
        Assert.True(t.Value.Done);
        Assert.Equal("20260601T090000Z", t.Value.LastModified);
    }

    [Fact]
    public void Parse_desescapa_y_respeta_parametros_y_plegado()
    {
        // Plegado a mitad de palabra (caf|é): el unfolding (RFC 5545) quita el CRLF + el espacio inicial.
        var ics = "BEGIN:VTODO\r\nUID:x\r\nSUMMARY;LANGUAGE=es:Pan\\, leche y caf\r\n é\r\nSTATUS:NEEDS-ACTION\r\nEND:VTODO";
        var t = IcalTodo.Parse(ics);
        Assert.NotNull(t);
        Assert.Equal("Pan, leche y café", t!.Value.Summary);   // unfold + unescape
        Assert.False(t.Value.Done);
    }

    [Fact]
    public void Parse_sin_vtodo_devuelve_null()
        => Assert.Null(IcalTodo.Parse("BEGIN:VCALENDAR\r\nEND:VCALENDAR"));
}

public class CalDavXmlTests
{
    [Fact]
    public void CurrentUserPrincipal_extrae_href()
    {
        var xml = @"<multistatus xmlns='DAV:'><response><href>/</href><propstat><prop>
            <current-user-principal><href>/123/principal/</href></current-user-principal>
            </prop><status>HTTP/1.1 200 OK</status></propstat></response></multistatus>";
        Assert.Equal("/123/principal/", CalDavXml.CurrentUserPrincipal(xml));
    }

    [Fact]
    public void CalendarHomeSet_extrae_href()
    {
        var xml = @"<multistatus xmlns='DAV:' xmlns:c='urn:ietf:params:xml:ns:caldav'>
            <response><href>/123/principal/</href><propstat><prop>
            <c:calendar-home-set><href>https://p01-caldav.icloud.com/123/calendars/</href></c:calendar-home-set>
            </prop></propstat></response></multistatus>";
        Assert.Equal("https://p01-caldav.icloud.com/123/calendars/", CalDavXml.CalendarHomeSet(xml));
    }

    [Fact]
    public void ParseMultistatus_detecta_colecciones_vtodo()
    {
        var xml = @"<multistatus xmlns='DAV:' xmlns:c='urn:ietf:params:xml:ns:caldav'>
            <response><href>/123/calendars/reminders/</href><propstat><prop>
                <displayname>Recordatorios</displayname>
                <c:supported-calendar-component-set><c:comp name='VTODO'/></c:supported-calendar-component-set>
            </prop></propstat></response>
            <response><href>/123/calendars/home/</href><propstat><prop>
                <displayname>Calendario</displayname>
                <c:supported-calendar-component-set><c:comp name='VEVENT'/></c:supported-calendar-component-set>
            </prop></propstat></response></multistatus>";
        var rs = CalDavXml.ParseMultistatus(xml);
        Assert.Equal(2, rs.Count);
        var reminders = rs.First(r => r.Href.Contains("reminders"));
        Assert.True(reminders.SupportsVTodo);
        Assert.Equal("Recordatorios", reminders.DisplayName);
        Assert.False(rs.First(r => r.Href.Contains("home")).SupportsVTodo);
    }

    [Fact]
    public void ParseMultistatus_lee_etag_y_calendar_data()
    {
        var xml = @"<multistatus xmlns='DAV:' xmlns:c='urn:ietf:params:xml:ns:caldav'>
            <response><href>/123/calendars/reminders/task1.ics</href><propstat><prop>
                <getetag>""etag-123""</getetag>
                <c:calendar-data>BEGIN:VTODO
UID:task1
SUMMARY:Hola
END:VTODO</c:calendar-data>
            </prop></propstat></response></multistatus>";
        var r = Assert.Single(CalDavXml.ParseMultistatus(xml));
        Assert.Equal("\"etag-123\"", r.Etag);
        Assert.Contains("UID:task1", r.CalendarData);
    }

    [Fact]
    public void ParseMultistatus_distingue_calendar_real_de_outbox()
    {
        // La lista real es <calendar>; el outbox de scheduling NO, aunque anuncie VTODO. (#64)
        var xml = @"<multistatus xmlns='DAV:' xmlns:c='urn:ietf:params:xml:ns:caldav'>
            <response><href>/123/calendars/reminders/</href><propstat><prop>
                <resourcetype><collection/><c:calendar/></resourcetype>
                <c:supported-calendar-component-set><c:comp name='VTODO'/></c:supported-calendar-component-set>
            </prop></propstat></response>
            <response><href>/123/calendars/outbox/</href><propstat><prop>
                <resourcetype><collection/><c:schedule-outbox/></resourcetype>
                <c:supported-calendar-component-set><c:comp name='VTODO'/></c:supported-calendar-component-set>
            </prop></propstat></response></multistatus>";
        var rs = CalDavXml.ParseMultistatus(xml);
        var cal = rs.First(r => r.Href.Contains("reminders"));
        var outbox = rs.First(r => r.Href.Contains("outbox"));
        Assert.True(cal.IsCalendar);
        Assert.True(cal.SupportsVTodo);
        Assert.False(outbox.IsCalendar);   // schedule-outbox: se descarta como lista
    }

    [Fact]
    public void ParseMultistatus_xml_invalido_no_revienta()
        => Assert.Empty(CalDavXml.ParseMultistatus("no es xml"));
}
