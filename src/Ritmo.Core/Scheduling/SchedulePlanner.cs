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
    private readonly IReadOnlySet<string> _focus;
    private readonly IReadOnlyList<OneOffSession> _oneOffs;

    /// <summary>
    /// Ids de categoría que disparan concentración por defecto (fallback para tests y
    /// llamadas sin contexto). En producción se pasan los ids reales de
    /// <c>AppSettings.Categories</c> con <c>IsFocus</c>. #83
    /// </summary>
    public static readonly IReadOnlySet<string> DefaultFocusCategoryIds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Tecnico", "Legislacion", "Ingles", "Tests", "Simulacro" };

    public SchedulePlanner(WeeklySchedule schedule, IReadOnlySet<string>? focusCategoryIds = null,
                           IReadOnlyList<OneOffSession>? oneOffs = null)
    {
        _schedule = schedule;
        _focus = focusCategoryIds ?? DefaultFocusCategoryIds;
        _oneOffs = oneOffs ?? [];
    }

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
                // ni las categorías que no son de concentración (Descanso, Personal, PorDefinir…).
                // Solo se ven en el calendario; sus avisos, si los tienen, suenan igual.
                if (!s.IsTentative && _focus.Contains(s.CategoryId) && start >= from && start < until)
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

        // Sesiones provisionales (one-off): ocurren en una FECHA concreta (#103/#128). Se
        // generan igual que las recurrentes — inicio (solo focus, no tentativo) + un evento
        // por aviso previo (los avisos suenan aunque no disparen concentración).
        foreach (var o in _oneOffs)
        {
            var oneStart = o.Date.ToDateTime(o.Start);

            if (!o.IsTentative && _focus.Contains(o.CategoryId) && oneStart >= from && oneStart < until)
                events.Add(new PlannedEvent
                {
                    At = oneStart,
                    Type = PlannedEventType.SessionStart,
                    Session = o.AsSession(),
                    SessionStartAt = oneStart
                });

            foreach (var alert in o.PreAlerts)
            {
                var alertAt = oneStart - TimeSpan.FromMinutes(alert.MinutesBefore);
                if (alertAt >= from && alertAt < until)
                    events.Add(new PlannedEvent
                    {
                        At = alertAt,
                        Type = PlannedEventType.PreAlert,
                        Session = o.AsSession(),
                        MinutesBefore = alert.MinutesBefore,
                        SessionStartAt = oneStart
                    });
            }
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
            // ni las categorías que no son de concentración (Descanso, Personal…).
            if (s.IsTentative || !_focus.Contains(s.CategoryId))
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
