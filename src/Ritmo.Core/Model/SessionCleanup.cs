using Ritmo.Core.Persistence;

namespace Ritmo.Core.Model;

/// <summary>
/// Limpieza de "hijos huérfanos" de las sesiones (#138). Cuando se BORRA una sesión recurrente,
/// sus excepciones (#137: «no realizada / parcial»), que se asocian por <see cref="SessionKey"/>,
/// dejan de tener una sesión a la que aplicarse: quedan inertes en el almacenamiento y, si más
/// tarde se recrea una sesión con la misma clave, reaparecerían marcas viejas inesperadas.
///
/// Estas funciones son PURAS: dado el estado YA SIN la sesión borrada, devuelven la lista de
/// excepciones podada (solo se conservan las que siguen apuntando a una sesión recurrente viva).
///
/// Nota: se podan solo en operaciones de BORRADO, no al editar/redimensionar (ahí la sesión sigue
/// viva aunque cambie su clave). Los post-its (<see cref="StudyNote.SessionTitle"/>) NO se tocan:
/// contienen texto del usuario y borrarlos sería pérdida de datos.
/// </summary>
public static class SessionCleanup
{
    /// <summary>Todas las sesiones recurrentes vivas: horario suelto + todas las fases del plan.</summary>
    public static IEnumerable<StudySession> AllRecurringSessions(AppSettings s)
        => s.Schedule.Sessions.Concat(s.Plan.Phases.SelectMany(p => p.Schedule.Sessions));

    /// <summary>
    /// Devuelve las excepciones que siguen apuntando a una sesión recurrente viva. Las huérfanas
    /// (cuya <see cref="SessionKey"/> ya no existe en ninguna sesión) se descartan.
    /// </summary>
    public static IReadOnlyList<SessionException> PruneOrphanExceptions(
        IEnumerable<StudySession> liveSessions,
        IReadOnlyList<SessionException> exceptions)
    {
        var liveKeys = new HashSet<string>(liveSessions.Select(SessionKey.For));
        return exceptions.Where(e => liveKeys.Contains(e.SessionKey)).ToList();
    }

    /// <summary>
    /// Aplica la poda de excepciones huérfanas sobre un <see cref="AppSettings"/> ya actualizado
    /// (sin la sesión borrada). Devuelve el estado con las excepciones limpias.
    /// </summary>
    public static AppSettings PruneOrphans(AppSettings updated)
    {
        var pruned = PruneOrphanExceptions(AllRecurringSessions(updated), updated.SessionExceptions);
        return pruned.Count == updated.SessionExceptions.Count
            ? updated
            : updated with { SessionExceptions = pruned };
    }
}
