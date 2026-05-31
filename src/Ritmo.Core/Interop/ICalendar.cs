using System.Globalization;
using System.Text;
using Ritmo.Core.Model;

namespace Ritmo.Core.Interop;

/// <summary>
/// Import/export del horario en formato iCalendar (.ics), el estándar que usan
/// Google Calendar, Outlook y Apple Calendar. Cada sesión semanal se representa
/// como un VEVENT recurrente (RRULE:FREQ=WEEKLY;BYDAY=..). Texto plano, sin
/// dependencias externas.
/// </summary>
public static class ICalendar
{
    // BYDAY de iCalendar por día de la semana.
    private static readonly Dictionary<DayOfWeek, string> ByDay = new()
    {
        [DayOfWeek.Monday] = "MO", [DayOfWeek.Tuesday] = "TU", [DayOfWeek.Wednesday] = "WE",
        [DayOfWeek.Thursday] = "TH", [DayOfWeek.Friday] = "FR",
        [DayOfWeek.Saturday] = "SA", [DayOfWeek.Sunday] = "SU"
    };
    private static readonly Dictionary<string, DayOfWeek> DayByCode =
        ByDay.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>
    /// Exporta un horario semanal a .ics. <paramref name="anchorMonday"/> es el
    /// lunes a partir del cual empiezan las recurrencias (por defecto, una fecha
    /// fija de referencia). Si se dan from/to, se acota la recurrencia.
    /// </summary>
    public static string Export(WeeklySchedule schedule, DateOnly? from = null, DateOnly? to = null)
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//Ritmo//ES\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");

        // Semana de referencia: la del 'from' o un lunes fijo (2024-01-01 es lunes).
        var baseMonday = WeekStart(from ?? new DateOnly(2024, 1, 1));

        int seq = 0;
        foreach (var s in schedule.Sessions)
        {
            var dayOffset = ((int)s.Day - (int)DayOfWeek.Monday + 7) % 7;
            var firstDate = baseMonday.AddDays(dayOffset);
            var dtStart = firstDate.ToDateTime(s.Start);
            var dtEnd = dtStart + s.Duration;

            sb.Append("BEGIN:VEVENT\r\n");
            sb.Append($"UID:ritmo-{seq}-{s.Day}-{s.Start:HHmm}@ritmo\r\n");
            sb.Append($"SUMMARY:{Escape(s.Title)}\r\n");
            sb.Append($"DTSTART:{Stamp(dtStart)}\r\n");
            sb.Append($"DTEND:{Stamp(dtEnd)}\r\n");
            var rrule = $"RRULE:FREQ=WEEKLY;BYDAY={ByDay[s.Day]}";
            if (to is { } end)
                rrule += $";UNTIL={Stamp(end.ToDateTime(new TimeOnly(23, 59)))}";
            sb.Append(rrule + "\r\n");
            if (!string.IsNullOrEmpty(s.CategoryId))
                sb.Append($"CATEGORIES:{s.CategoryId}\r\n");
            if (s.IsTentative)
                sb.Append("STATUS:TENTATIVE\r\n");
            sb.Append("END:VEVENT\r\n");
            seq++;
        }

        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }

    /// <summary>
    /// Importa un .ics y reconstruye un horario semanal a partir de los VEVENT
    /// con recurrencia semanal (RRULE FREQ=WEEKLY). Los eventos sin BYDAY usan
    /// el día del DTSTART. Ignora lo que no sepa interpretar (robusto).
    /// </summary>
    public static WeeklySchedule Import(string ics)
    {
        var sessions = new List<StudySession>();
        foreach (var block in SplitEvents(ics))
        {
            var props = ParseProps(block);
            if (!props.TryGetValue("DTSTART", out var dtStartRaw)) continue;
            if (!TryParseStamp(dtStartRaw, out var dtStart)) continue;

            props.TryGetValue("RRULE", out var rrule);
            // Solo recurrencias semanales (o, si no hay RRULE, lo tratamos como semanal del día del DTSTART).
            if (rrule is not null && !rrule.Contains("FREQ=WEEKLY", StringComparison.OrdinalIgnoreCase))
                continue;

            var day = DayFromRRuleOrDate(rrule, dtStart);

            TimeSpan duration = TimeSpan.FromHours(1);
            if (props.TryGetValue("DTEND", out var dtEndRaw) && TryParseStamp(dtEndRaw, out var dtEnd) && dtEnd > dtStart)
                duration = dtEnd - dtStart;

            var title = props.TryGetValue("SUMMARY", out var sum) ? Unescape(sum) : "(sin título)";
            var categoryId = props.TryGetValue("CATEGORIES", out var cat) && !string.IsNullOrWhiteSpace(cat)
                ? cat.Trim() : CategoryIds.Other;
            var tentative = props.TryGetValue("STATUS", out var st)
                && st.Trim().Equals("TENTATIVE", StringComparison.OrdinalIgnoreCase);

            sessions.Add(new StudySession
            {
                Title = title,
                Day = day,
                Start = TimeOnly.FromDateTime(dtStart),
                Duration = duration,
                CategoryId = categoryId,
                IsTentative = tentative
            });
        }
        return new WeeklySchedule { Sessions = sessions };
    }

    /// <summary>
    /// Lee eventos CON FECHA de un .ics dentro de [from, to] (compromisos puntuales,
    /// no el horario recurrente). Soporta eventos sueltos, todo-el-día y recurrencia
    /// simple FREQ=WEEKLY/DAILY (con BYDAY y UNTIL). Las horas se tratan como locales
    /// (best-effort; ignora zonas horarias). #112
    /// </summary>
    public static IReadOnlyList<CalendarEvent> ImportEvents(string ics, DateOnly from, DateOnly to, string? calendar = null)
    {
        var result = new List<CalendarEvent>();
        if (string.IsNullOrWhiteSpace(ics)) return result;
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(new TimeOnly(23, 59, 59));

        foreach (var block in SplitEvents(ics))
        {
            var props = ParseProps(block);
            if (!props.TryGetValue("DTSTART", out var dtStartRaw)) continue;
            if (!TryParseDateOrTime(dtStartRaw, out var start, out var allDay)) continue;

            DateTime end;
            if (props.TryGetValue("DTEND", out var dtEndRaw) && TryParseDateOrTime(dtEndRaw, out var e, out _)) end = e;
            else end = start + (allDay ? TimeSpan.FromDays(1) : TimeSpan.FromHours(1));

            var title = props.TryGetValue("SUMMARY", out var sum) ? Unescape(sum) : "(sin título)";
            props.TryGetValue("RRULE", out var rrule);

            if (string.IsNullOrEmpty(rrule))
            {
                if (start <= toDt && end >= fromDt)
                    result.Add(new CalendarEvent(title, start, end, allDay, calendar));
            }
            else
            {
                ExpandRecurring(rrule!, start, end, fromDt, toDt, title, allDay, calendar, result);
            }
        }
        return result;
    }

    private static void ExpandRecurring(string rrule, DateTime start, DateTime end, DateTime fromDt, DateTime toDt,
        string title, bool allDay, string? cal, List<CalendarEvent> result)
    {
        var parts = ParseRRule(rrule);
        parts.TryGetValue("FREQ", out var freq);
        var duration = end - start;
        var until = toDt;
        if (parts.TryGetValue("UNTIL", out var untilRaw) && TryParseDateOrTime(untilRaw, out var u, out _) && u < until)
            until = u;

        bool weekly = string.Equals(freq, "WEEKLY", StringComparison.OrdinalIgnoreCase);
        bool daily = string.Equals(freq, "DAILY", StringComparison.OrdinalIgnoreCase);
        if (!weekly && !daily)
        {
            if (start <= toDt && end >= fromDt) result.Add(new CalendarEvent(title, start, end, allDay, cal));
            return;
        }

        var days = new List<DayOfWeek>();
        if (weekly && parts.TryGetValue("BYDAY", out var byday))
            foreach (var code in byday.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var c = new string(code.Trim().Reverse().TakeWhile(char.IsLetter).Reverse().ToArray()).ToUpperInvariant();
                if (DayByCode.TryGetValue(c, out var d)) days.Add(d);
            }
        if (weekly && days.Count == 0) days.Add(start.DayOfWeek);

        var first = fromDt.Date > start.Date ? fromDt.Date : start.Date;
        int guard = 0;
        for (var date = first; date <= until.Date && guard < 500; date = date.AddDays(1), guard++)
        {
            if (weekly && !days.Contains(date.DayOfWeek)) continue;
            var occStart = date + start.TimeOfDay;
            if (occStart > toDt) break;
            if (occStart.Add(duration) >= fromDt)
                result.Add(new CalendarEvent(title, occStart, occStart.Add(duration), allDay, cal));
        }
    }

    private static Dictionary<string, string> ParseRRule(string rrule)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var val = rrule.Contains(':') ? rrule[(rrule.IndexOf(':') + 1)..] : rrule;
        foreach (var p in val.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = p.IndexOf('=');
            if (eq > 0) dict[p[..eq].Trim()] = p[(eq + 1)..].Trim();
        }
        return dict;
    }

    /// <summary>Parsea DTSTART/DTEND: fecha-hora (yyyyMMddTHHmmss[Z]) o fecha (yyyyMMdd = todo el día).</summary>
    private static bool TryParseDateOrTime(string raw, out DateTime dt, out bool allDay)
    {
        allDay = false;
        var value = raw.Contains(':') ? raw[(raw.LastIndexOf(':') + 1)..] : raw;
        value = value.TrimEnd('Z').Trim();
        if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return true;
        if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            allDay = true;
            return true;
        }
        return false;
    }

    // ---------- helpers ----------

    private static DateOnly WeekStart(DateOnly d)
    {
        var offset = ((int)d.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return d.AddDays(-offset);
    }

    private static DayOfWeek DayFromRRuleOrDate(string? rrule, DateTime dtStart)
    {
        if (rrule is not null)
        {
            var idx = rrule.IndexOf("BYDAY=", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var rest = rrule[(idx + 6)..];
                var code = new string(rest.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
                if (DayByCode.TryGetValue(code, out var d)) return d;
            }
        }
        return dtStart.DayOfWeek;
    }

    // Formato de fecha-hora "flotante" local: 20260601T090000
    private static string Stamp(DateTime dt) => dt.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

    private static bool TryParseStamp(string raw, out DateTime dt)
    {
        // Quita parámetros (";TZID=..:") y la Z final si la hay.
        var value = raw.Contains(':') ? raw[(raw.LastIndexOf(':') + 1)..] : raw;
        value = value.TrimEnd('Z');
        return DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
    }

    private static IEnumerable<string> SplitEvents(string ics)
    {
        var unfolded = Unfold(ics);
        var lines = unfolded.Split('\n');
        var current = new StringBuilder();
        var inside = false;
        foreach (var lineRaw in lines)
        {
            var line = lineRaw.TrimEnd('\r');
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                inside = true; current.Clear(); continue;
            }
            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                inside = false; yield return current.ToString(); continue;
            }
            if (inside) current.Append(line).Append('\n');
        }
    }

    private static Dictionary<string, string> ParseProps(string block)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var keyPart = line[..colon];
            var value = line[(colon + 1)..].TrimEnd('\r');
            // La clave es lo anterior a cualquier ';' (parámetros).
            var semi = keyPart.IndexOf(';');
            var key = (semi < 0 ? keyPart : keyPart[..semi]).Trim().ToUpperInvariant();
            // Para DTSTART/DTEND guardamos la línea completa (puede traer TZID).
            dict[key] = (key is "DTSTART" or "DTEND") ? line : value;
        }
        return dict;
    }

    // Desdobla líneas plegadas (continuación con espacio/tab al inicio), según RFC 5545.
    private static string Unfold(string ics) =>
        ics.Replace("\r\n ", "").Replace("\r\n\t", "").Replace("\n ", "").Replace("\n\t", "");

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");

    private static string Unescape(string s) =>
        s.Replace("\\n", "\n").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");
}
