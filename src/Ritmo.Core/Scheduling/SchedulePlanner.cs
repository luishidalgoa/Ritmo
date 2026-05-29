using Ritmo.Core.Model;

namespace Ritmo.Core.Scheduling;

/// <summary>
/// Convierte un horario semanal (recurrente) en eventos absolutos en el tiempo.
/// Es PURO y determinista: recibe el "ahora" como parámetro, así se testea sin
/// depender del reloj real. El host (servicio en segundo plano) le pregunta
/// "¿qué viene a continuación?" y programa timers en consecuencia.
/// </summary>
public sealed class SchedulePlanner
{
    private readonly WeeklySchedule _schedule;

    public SchedulePlanner(WeeklySchedule schedule) => _schedule = schedule;

    /// <summary>
    /// Expande cada sesión semanal a sus ocurrencias absolutas dentro de
    /// [from, from + horizon), generando un evento de inicio y uno por aviso previo.
    /// Devuelve la lista ordenada por fecha/hora ascendente.
    /// </summary>
    public IReadOnlyList<PlannedEvent> GetEvents(DateTime from, TimeSpan horizon)
    {
        var until = from + horizon;
        var events = new List<PlannedEvent>();

        // Recorremos día a día desde 'from' hasta cubrir el horizonte.
        // Empezamos en la medianoche del día de 'from' para no perder sesiones de hoy.
        var dayCursor = from.Date;
        while (dayCursor <= until)
        {
            foreach (var s in _schedule.Sessions)
            {
                if (s.Day != dayCursor.DayOfWeek)
                    continue;

                var start = dayCursor + s.Start.ToTimeSpan();

                // Evento de inicio de sesión.
                // No disparan inicio automático de concentración: los bloques tentativos
                // ni los tipos que no son de estudio (Descanso, Personal, PorDefinir…).
                // Solo se ven en el calendario; sus avisos, si los tienen, suenan igual.
                if (!s.IsTentative && s.Kind.IsFocusKind() && start >= from && start < until)
                {
                    events.Add(new PlannedEvent
                    {
                        At = start,
                        Type = PlannedEventType.SessionStart,
                        Session = s,
                        SessionStartAt = start
                    });
                }

                // Un evento por cada aviso previo.
                foreach (var alert in s.PreAlerts)
                {
                    var alertAt = start - TimeSpan.FromMinutes(alert.MinutesBefore);
                    if (alertAt >= from && alertAt < until)
                    {
                        events.Add(new PlannedEvent
                        {
                            At = alertAt,
                            Type = PlannedEventType.PreAlert,
                            Session = s,
                            MinutesBefore = alert.MinutesBefore,
                            SessionStartAt = start
                        });
                    }
                }
            }
            dayCursor = dayCursor.AddDays(1);
        }

        // Orden estable: por fecha, y a igualdad, el aviso antes que el inicio.
        return events
            .OrderBy(e => e.At)
            .ThenBy(e => e.Type == PlannedEventType.SessionStart ? 1 : 0)
            .ToList();
    }

    /// <summary>
    /// Devuelve el siguiente evento estrictamente posterior a 'from'
    /// (o null si no hay ninguno dentro del horizonte indicado).
    /// </summary>
    public PlannedEvent? GetNextEvent(DateTime from, TimeSpan? horizon = null)
    {
        var h = horizon ?? TimeSpan.FromDays(8);
        return GetEvents(from, h).FirstOrDefault(e => e.At > from);
    }

    /// <summary>
    /// La próxima sesión de HOY que empieza estrictamente después de la hora dada
    /// (la siguiente cosa del día, sea del tipo que sea). Null si no queda nada hoy.
    /// Útil para la vista "Hoy / Ahora": "después → …".
    /// </summary>
    public StudySession? GetNextSessionToday(DateTime now)
    {
        var nowTime = TimeOnly.FromDateTime(now);
        return _schedule.Sessions
            .Where(s => s.Day == now.DayOfWeek && s.Start > nowTime)
            .OrderBy(s => s.Start)
            .FirstOrDefault();
    }

    /// <summary>
    /// Indica qué sesión está activa en el instante dado (inicio &lt;= now &lt; fin),
    /// o null si en ese momento no hay ninguna sesión en curso.
    /// </summary>
    public StudySession? GetActiveSession(DateTime now)
    {
        var nowTime = TimeOnly.FromDateTime(now);
        foreach (var s in _schedule.Sessions)
        {
            if (s.Day != now.DayOfWeek)
                continue;
            // No cuentan como sesión activa (no disparan concentración) los tentativos
            // ni los tipos que no son de estudio (Descanso, Personal…).
            if (s.IsTentative || !s.Kind.IsFocusKind())
                continue;
            // Sesión que no cruza medianoche (caso normal del horario 08–20).
            if (s.Start <= s.End)
            {
                if (nowTime >= s.Start && nowTime < s.End)
                    return s;
            }
            else
            {
                // Cruza medianoche: activa si está después del inicio o antes del fin.
                if (nowTime >= s.Start || nowTime < s.End)
                    return s;
            }
        }
        return null;
    }
}
