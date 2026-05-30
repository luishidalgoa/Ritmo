using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ritmo.Core.Interop;
using Ritmo.Core.Model;

namespace Ritmo.Core.Scheduling;

/// <summary>
/// Detección pura de solapamientos entre las sesiones recurrentes del horario y
/// los eventos con fecha de los calendarios externos (#114). Sirve para que el
/// usuario vea ambos y decida cuál priorizar. Sin UI ni IO: testable en aislado.
/// </summary>
public static class OverlapResolver
{
    /// <summary>
    /// Clave estable de un evento de calendario, para recordar la prioridad elegida
    /// aunque se vuelva a descargar el calendario. Combina calendario + título +
    /// instante de inicio (formato ordenable invariante).
    /// </summary>
    public static string EventKey(CalendarEvent ev)
        => $"{ev.Calendar ?? ""}|{ev.Title}|{ev.Start.ToString("s", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// ¿El evento (con fecha) pisa a la sesión recurrente en la fecha indicada?
    /// Los eventos de todo el día son informativos (festivos, cumpleaños…): NO se
    /// consideran conflicto, porque no compiten por una franja concreta.
    /// </summary>
    public static bool Overlaps(StudySession session, DateOnly date, CalendarEvent ev)
    {
        if (ev.AllDay) return false;
        if (DateOnly.FromDateTime(ev.Start) != date) return false;
        if (session.Day != date.DayOfWeek) return false;

        var evStart = TimeOnly.FromDateTime(ev.Start);
        var evDur = ev.End - ev.Start;
        if (evDur <= TimeSpan.Zero) evDur = TimeSpan.FromMinutes(1);   // evento puntual
        return ScheduleMath.TimesOverlap(session.Start, session.Duration, evStart, evDur);
    }

    /// <summary>Eventos del calendario que pisan a una sesión concreta en una fecha.</summary>
    public static IReadOnlyList<CalendarEvent> EventsOverlapping(
        StudySession session, DateOnly date, IEnumerable<CalendarEvent> events)
        => events.Where(ev => Overlaps(session, date, ev)).ToList();

    /// <summary>Sesiones del horario semanal que pisa un evento concreto (en su propia fecha).</summary>
    public static IReadOnlyList<StudySession> SessionsOverlapping(
        CalendarEvent ev, IEnumerable<StudySession> sessions)
    {
        if (ev.AllDay) return [];
        var date = DateOnly.FromDateTime(ev.Start);
        return sessions.Where(s => Overlaps(s, date, ev)).ToList();
    }
}
