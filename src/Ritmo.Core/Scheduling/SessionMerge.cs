using Ritmo.Core.Model;

namespace Ritmo.Core.Scheduling;

/// <summary>
/// Un grupo de sesiones idénticas en días CONTIGUOS, para pintarlas como una sola
/// tarjeta que abarca varias columnas. <see cref="FirstDayIndex"/> y <see cref="DaySpan"/>
/// se refieren al orden de días dado a <see cref="SessionMerge.Merge"/>.
/// </summary>
public sealed record SessionGroup(
    StudySession Representative,
    int FirstDayIndex,
    int DaySpan,
    IReadOnlyList<StudySession> Members);

/// <summary>
/// Agrupa sesiones idénticas (mismo título, tipo, inicio, duración y «provisional»)
/// que caen en días CONTIGUOS, en grupos que el horario pinta como una tarjeta única
/// con ColumnSpan. PURO y testable. Las sesiones que no comparten señas, o que no
/// son contiguas, quedan como grupos de un solo día.
/// </summary>
public static class SessionMerge
{
    private static (string, string, TimeOnly, TimeSpan, bool) Signature(StudySession s)
        => (s.Title.Trim(), s.CategoryId, s.Start, s.Duration, s.IsTentative);

    public static IReadOnlyList<SessionGroup> Merge(
        IReadOnlyList<StudySession> sessions, IReadOnlyList<DayOfWeek> dayOrder)
    {
        var dayIndex = new Dictionary<DayOfWeek, int>();
        for (int i = 0; i < dayOrder.Count; i++) dayIndex[dayOrder[i]] = i;

        var groups = new List<SessionGroup>();

        foreach (var bySig in sessions.GroupBy(Signature))
        {
            // Sesiones de esta firma, ordenadas por la posición de su día en dayOrder.
            var byDay = bySig
                .Where(s => dayIndex.ContainsKey(s.Day))
                .Select(s => (idx: dayIndex[s.Day], session: s))
                .OrderBy(x => x.idx)
                .ToList();

            int i = 0;
            while (i < byDay.Count)
            {
                int runStart = i;
                // Extiende mientras los días sean estrictamente consecutivos (idx+1).
                while (i + 1 < byDay.Count && byDay[i + 1].idx == byDay[i].idx + 1) i++;

                var members = byDay.GetRange(runStart, i - runStart + 1).Select(x => x.session).ToList();
                groups.Add(new SessionGroup(members[0], byDay[runStart].idx, members.Count, members));
                i++;
            }
        }

        // Orden estable para pintar: por día y luego por hora de inicio.
        return groups.OrderBy(g => g.FirstDayIndex).ThenBy(g => g.Representative.Start).ToList();
    }
}
