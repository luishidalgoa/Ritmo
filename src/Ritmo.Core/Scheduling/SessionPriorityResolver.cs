using Ritmo.Core.Model;

namespace Ritmo.Core.Scheduling;

/// <summary>Estado visual de una sesión dentro de un solapamiento (#149).</summary>
public enum ConflictState
{
    /// <summary>No choca con nadie, o su grupo de solape no tiene ninguna prioritaria: se pinta normal.</summary>
    Normal,
    /// <summary>Marcada como prioritaria dentro de un solape: se destaca.</summary>
    Priority,
    /// <summary>No marcada, pero comparte solape con al menos una prioritaria: recede (se atenúa).</summary>
    Receded
}

/// <summary>
/// Resolución pura de la prioridad entre sesiones que se solapan en un mismo día (#149).
/// Es el equivalente sesión↔sesión del resolver horario↔calendario: el usuario marca
/// cuáles son "prioritarias" y las que NO lo son, dentro de un mismo grupo de solape,
/// recede. Sin UI ni IO: testable en aislado.
/// </summary>
public static class SessionPriorityResolver
{
    /// <summary>
    /// Dadas las sesiones de UN día (cada una con su clave estable) y el conjunto de claves
    /// marcadas como prioritarias, devuelve el estado de cada clave:
    /// <list type="bullet">
    /// <item><see cref="ConflictState.Normal"/>: sola en su franja, o su grupo de solape no tiene ninguna prioritaria.</item>
    /// <item><see cref="ConflictState.Priority"/>: marcada prioritaria Y dentro de un grupo con solape real.</item>
    /// <item><see cref="ConflictState.Receded"/>: no marcada, pero su grupo de solape tiene alguna prioritaria.</item>
    /// </list>
    /// Las sesiones se agrupan por componentes conexas de solape temporal (igual que
    /// <see cref="OverlapLanes"/>), así dos conflictos distintos del mismo día no se contaminan.
    /// </summary>
    public static IReadOnlyDictionary<string, ConflictState> Resolve(
        IReadOnlyList<(StudySession Session, string Key)> daySessions,
        IReadOnlySet<string> priorityKeys)
    {
        var result = new Dictionary<string, ConflictState>();
        int n = daySessions.Count;
        if (n == 0) return result;

        // Componentes conexas por solape temporal (union-find), idéntico a OverlapLanes.
        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (ScheduleMath.TimesOverlap(daySessions[i].Session.Start, daySessions[i].Session.Duration,
                                              daySessions[j].Session.Start, daySessions[j].Session.Duration))
                    parent[Find(i)] = Find(j);

        var clusters = new Dictionary<int, List<int>>();
        for (int i = 0; i < n; i++)
        {
            int r = Find(i);
            if (!clusters.TryGetValue(r, out var list)) clusters[r] = list = new List<int>();
            list.Add(i);
        }

        foreach (var members in clusters.Values)
        {
            // Sin solape real (un solo miembro) -> normal.
            if (members.Count <= 1) { result[daySessions[members[0]].Key] = ConflictState.Normal; continue; }

            bool anyPriority = members.Any(i => priorityKeys.Contains(daySessions[i].Key));
            foreach (var i in members)
            {
                var key = daySessions[i].Key;
                result[key] = priorityKeys.Contains(key)
                    ? ConflictState.Priority
                    : (anyPriority ? ConflictState.Receded : ConflictState.Normal);
            }
        }

        return result;
    }
}
