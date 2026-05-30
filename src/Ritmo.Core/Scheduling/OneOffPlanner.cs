using System;
using System.Collections.Generic;
using System.Linq;
using Ritmo.Core.Model;

namespace Ritmo.Core.Scheduling;

/// <summary>
/// Resuelve qué sesión PROVISIONAL (con fecha, #103) está activa o es la siguiente
/// "ahora". Puro y testable. El host combina esto con el horario recurrente: una
/// provisional que cubre el instante actual tiene PRIORIDAD (es lo extraordinario
/// que el usuario decidió hacer hoy).
/// </summary>
public static class OneOffPlanner
{
    /// <summary>La provisional que cubre el instante <paramref name="now"/> (o null).</summary>
    public static OneOffSession? ActiveAt(IEnumerable<OneOffSession> oneOffs, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        int nowMin = now.Hour * 60 + now.Minute;   // minutos desde medianoche (sin envolver)
        return oneOffs
            .Where(o => o.Date == today)
            .Where(o =>
            {
                int s = o.Start.Hour * 60 + o.Start.Minute;
                int e = s + (int)o.Duration.TotalMinutes;   // puede pasar de 1440 si cruza medianoche
                return nowMin >= s && nowMin < e;
            })
            .OrderBy(o => o.Start)
            .FirstOrDefault();
    }

    /// <summary>La siguiente provisional de HOY que empieza después de <paramref name="now"/> (o null).</summary>
    public static OneOffSession? NextToday(IEnumerable<OneOffSession> oneOffs, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var t = TimeOnly.FromDateTime(now);
        return oneOffs
            .Where(o => o.Date == today && o.Start > t)
            .OrderBy(o => o.Start)
            .FirstOrDefault();
    }
}
