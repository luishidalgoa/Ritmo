using Ritmo.Core.Model;

namespace Ritmo.Core.Scheduling;

/// <summary>Una sesión con su carril (columna) asignado y cuántos carriles tiene su grupo de solape.</summary>
public sealed record LaneAssignment(StudySession Session, int Lane, int LaneCount);

/// <summary>
/// Reparte en CARRILES (sub-columnas lado a lado) las sesiones de UN MISMO DÍA que se solapan en el
/// tiempo, al estilo de un calendario (#130). PURO y testable, sin UI.
///
/// Algoritmo clásico:
///  1. Asignación voraz de carril: cada sesión va al primer carril libre (cuyo final ≤ su inicio);
///     si no hay, abre uno nuevo. (Las no solapadas reusan el carril 0.)
///  2. Componentes conexas por solape (transitivo): dos sesiones quedan en el mismo grupo si se
///     solapan, directa o indirectamente.
///  3. <see cref="LaneAssignment.LaneCount"/> de cada sesión = nº de carriles de su grupo
///     (el ancho de columna se divide entre ese número y la sesión se coloca en su carril).
/// </summary>
public static class OverlapLanes
{
    public static IReadOnlyList<LaneAssignment> Assign(IReadOnlyList<StudySession> sameDaySessions)
    {
        var sorted = sameDaySessions
            .OrderBy(s => s.Start)
            .ThenBy(s => s.Duration)
            .ToList();
        int n = sorted.Count;
        if (n == 0) return [];

        // 1) Carril voraz: laneEnd[l] = hora de fin de lo último colocado en el carril l.
        var lane = new int[n];
        var laneEnd = new List<TimeOnly>();
        for (int i = 0; i < n; i++)
        {
            var s = sorted[i];
            var end = s.Start.Add(s.Duration);
            int chosen = -1;
            for (int l = 0; l < laneEnd.Count; l++)
                if (laneEnd[l] <= s.Start) { chosen = l; break; }
            if (chosen == -1) { chosen = laneEnd.Count; laneEnd.Add(end); }
            else laneEnd[chosen] = end;
            lane[i] = chosen;
        }

        // 2) Componentes conexas por solape (union-find).
        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int a, int b) { parent[Find(a)] = Find(b); }
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (ScheduleMath.TimesOverlap(sorted[i].Start, sorted[i].Duration, sorted[j].Start, sorted[j].Duration))
                    Union(i, j);

        // 3) Carriles por componente = max(lane)+1 dentro de la componente.
        var compCols = new Dictionary<int, int>();
        for (int i = 0; i < n; i++)
        {
            int r = Find(i);
            compCols[r] = Math.Max(compCols.GetValueOrDefault(r, 0), lane[i] + 1);
        }

        var result = new LaneAssignment[n];
        for (int i = 0; i < n; i++)
            result[i] = new LaneAssignment(sorted[i], lane[i], compCols[Find(i)]);
        return result;
    }
}
