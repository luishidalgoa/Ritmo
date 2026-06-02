using System;
using System.Collections.Generic;
using System.Linq;
using Ritmo.Core.Model;

namespace Ritmo.Core.Sync;

/// <summary>
/// Especificación PURA de un evento a publicar en un calendario externo (#112 Fase 2). Tiempos en
/// hora LOCAL "naïve" (sin zona); el host les pone la zona y el formato del proveedor. <see cref="Key"/>
/// es estable: identifica el evento entre publicaciones para actualizar en vez de duplicar.
/// </summary>
public sealed record CalendarEventSpec
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required DateTime Start { get; init; }
    public required DateTime End { get; init; }
    /// <summary>Si no es null, el evento se repite SEMANALMENTE en ese día; null = evento único.</summary>
    public DayOfWeek? WeeklyOn { get; init; }
    /// <summary>Fin (inclusivo) de la recurrencia, o null = indefinida. Solo aplica si <see cref="WeeklyOn"/> != null.</summary>
    public DateOnly? Until { get; init; }
}

/// <summary>
/// Convierte el horario de Ritmo (sesiones recurrentes por fases + extraordinarias) en eventos a
/// publicar (#112 Fase 2). Puro y testable: sin red ni zona horaria. Política:
/// - Cada sesión recurrente de una fase → un evento SEMANAL en su día, desde la primera ocurrencia
///   en/después del inicio de la fase, hasta el fin de la fase (UNTIL) si lo tiene.
/// - Cada sesión extraordinaria → un evento único en su fecha.
/// </summary>
public static class CalendarPublish
{
    public static IReadOnlyList<CalendarEventSpec> BuildSpecs(
        SchedulePlan plan, IReadOnlyList<OneOffSession> oneOffs)
    {
        var specs = new List<CalendarEventSpec>();

        foreach (var phase in plan.OrderedPhases)
        {
            foreach (var s in phase.Schedule.Sessions)
            {
                if (s.Duration <= TimeSpan.Zero) continue;
                var first = FirstOccurrence(phase.ValidFrom, s.Day);
                var start = first.ToDateTime(s.Start);
                specs.Add(new CalendarEventSpec
                {
                    Key = $"rec|{phase.Name}|{(int)s.Day}|{s.Title}|{s.Start:HH\\:mm}",
                    Title = s.Title,
                    Start = start,
                    End = start + s.Duration,
                    WeeklyOn = s.Day,
                    Until = phase.ValidTo
                });
            }
        }

        foreach (var o in oneOffs)
        {
            if (o.Duration <= TimeSpan.Zero) continue;
            var start = o.Date.ToDateTime(o.Start);
            specs.Add(new CalendarEventSpec
            {
                Key = $"one|{o.Id}",
                Title = o.Title,
                Start = start,
                End = start + o.Duration,
                WeeklyOn = null,
                Until = null
            });
        }

        return specs;
    }

    /// <summary>Primera fecha en/después de <paramref name="from"/> que cae en el día de la semana dado.</summary>
    public static DateOnly FirstOccurrence(DateOnly from, DayOfWeek day)
    {
        int diff = (((int)day - (int)from.DayOfWeek) % 7 + 7) % 7;
        return from.AddDays(diff);
    }
}
