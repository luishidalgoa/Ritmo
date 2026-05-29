using Ritmo.Core.Interop;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

public class ICalendarTests
{
    private static WeeklySchedule Sample() => new()
    {
        Sessions =
        [
            new StudySession {
                Title = "Técnico ▸ siguiente tema", Day = DayOfWeek.Monday,
                Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(2), Kind = StudyKind.Tecnico },
            new StudySession {
                Title = "Inglés", Day = DayOfWeek.Thursday,
                Start = new TimeOnly(9,0), Duration = TimeSpan.FromHours(2), Kind = StudyKind.Ingles }
        ]
    };

    [Fact]
    public void Export_genera_VCALENDAR_y_un_VEVENT_por_sesion()
    {
        var ics = ICalendar.Export(Sample());
        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("END:VCALENDAR", ics);
        Assert.Equal(2, Occurrences(ics, "BEGIN:VEVENT"));
        Assert.Contains("RRULE:FREQ=WEEKLY;BYDAY=MO", ics);
        Assert.Contains("RRULE:FREQ=WEEKLY;BYDAY=TH", ics);
        Assert.Contains("SUMMARY:Técnico ▸ siguiente tema", ics);
    }

    [Fact]
    public void Export_con_rango_anade_UNTIL()
    {
        var ics = ICalendar.Export(Sample(),
            from: new DateOnly(2026, 6, 1), to: new DateOnly(2026, 10, 31));
        Assert.Contains("UNTIL=20261031", ics);
    }

    [Fact]
    public void RoundTrip_conserva_dia_hora_duracion_y_tipo()
    {
        var ics = ICalendar.Export(Sample());
        var back = ICalendar.Import(ics);

        Assert.Equal(2, back.Sessions.Count);
        var lunes = back.Sessions.Single(s => s.Day == DayOfWeek.Monday);
        Assert.Equal(new TimeOnly(9, 0), lunes.Start);
        Assert.Equal(TimeSpan.FromHours(2), lunes.Duration);
        Assert.Equal(StudyKind.Tecnico, lunes.Kind);
        Assert.Equal("Técnico ▸ siguiente tema", lunes.Title);

        Assert.Contains(back.Sessions, s => s.Day == DayOfWeek.Thursday && s.Kind == StudyKind.Ingles);
    }

    [Fact]
    public void Import_de_ics_estilo_GoogleCalendar()
    {
        // .ics realista: con TZID, orden de propiedades distinto y una línea plegada.
        var ics =
            "BEGIN:VCALENDAR\r\n" +
            "VERSION:2.0\r\n" +
            "PRODID:-//Google Inc//Google Calendar 70.9054//EN\r\n" +
            "BEGIN:VEVENT\r\n" +
            "DTSTART;TZID=Europe/Madrid:20260602T160000\r\n" +
            "DTEND;TZID=Europe/Madrid:20260602T180000\r\n" +
            "RRULE:FREQ=WEEKLY;BYDAY=TU\r\n" +
            "SUMMARY:Tests de la tarde con un título muy largo que se pliega en\r\n" +
            "  dos líneas\r\n" +
            "END:VEVENT\r\n" +
            "END:VCALENDAR\r\n";

        var sch = ICalendar.Import(ics);
        var s = Assert.Single(sch.Sessions);
        Assert.Equal(DayOfWeek.Tuesday, s.Day);
        Assert.Equal(new TimeOnly(16, 0), s.Start);
        Assert.Equal(TimeSpan.FromHours(2), s.Duration);
        // La línea plegada se reunifica.
        Assert.Equal("Tests de la tarde con un título muy largo que se pliega en dos líneas", s.Title);
    }

    [Fact]
    public void Import_conserva_estado_TENTATIVE()
    {
        var schedule = new WeeklySchedule
        {
            Sessions = [ new StudySession {
                Title = "Quizá", Day = DayOfWeek.Friday,
                Start = new TimeOnly(16,0), Duration = TimeSpan.FromHours(1), IsTentative = true } ]
        };
        var back = ICalendar.Import(ICalendar.Export(schedule));
        Assert.True(back.Sessions.Single().IsTentative);
    }

    [Fact]
    public void Import_ignora_eventos_no_semanales()
    {
        var ics =
            "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
            "BEGIN:VEVENT\r\nDTSTART:20260601T090000\r\nDTEND:20260601T100000\r\n" +
            "RRULE:FREQ=DAILY\r\nSUMMARY:Diario\r\nEND:VEVENT\r\n" +
            "END:VCALENDAR\r\n";
        var sch = ICalendar.Import(ics);
        Assert.Empty(sch.Sessions); // no es semanal -> ignorado
    }

    private static int Occurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }
}
