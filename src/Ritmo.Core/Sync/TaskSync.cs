using System.Collections.Generic;
using System.Linq;

namespace Ritmo.Core.Sync;

/// <summary>Tarea local (Ritmo) vista por el motor de sync (#64).</summary>
public sealed record SyncLocalTask(string LocalId, string? ExternalId, string Text, bool Done, string? ExternalUpdated);

/// <summary>Tarea remota (Google) vista por el motor de sync (#64).</summary>
public sealed record SyncRemoteTask(string ExternalId, string Title, bool Done, string Updated);

/// <summary>
/// Plan de sincronización para UNA lista/bloque: qué hacer en cada lado. Lo calcula <see cref="TaskSync.Plan"/>
/// (puro) y lo ejecuta el host (llamadas a la API de Google + mutación local).
/// </summary>
public sealed record TaskSyncPlan
{
    /// <summary>Tareas locales SIN ExternalId → crearlas en Google.</summary>
    public IReadOnlyList<SyncLocalTask> PushNew { get; init; } = new List<SyncLocalTask>();
    /// <summary>Tareas locales con ExternalId que difieren (Google no cambió) → actualizar en Google.</summary>
    public IReadOnlyList<SyncLocalTask> PushUpdate { get; init; } = new List<SyncLocalTask>();
    /// <summary>Pares (localId, remoto) donde Google cambió desde la última sync → aplicar remoto a local.</summary>
    public IReadOnlyList<(string LocalId, SyncRemoteTask Remote)> PullUpdate { get; init; } = new List<(string, SyncRemoteTask)>();
    /// <summary>Tareas remotas sin equivalente local → crearlas en local.</summary>
    public IReadOnlyList<SyncRemoteTask> PullNew { get; init; } = new List<SyncRemoteTask>();
    /// <summary>LocalIds cuya ExternalId ya no existe en Google (borradas allí) → borrarlas en local.</summary>
    public IReadOnlyList<string> DeleteLocal { get; init; } = new List<string>();
}

/// <summary>
/// Motor PURO de reconciliación dos-direcciones entre tareas de Ritmo y de Google Tasks (#64).
/// Política (predecible, sin perder datos):
/// - Local sin ExternalId → se crea en Google.
/// - Par emparejado: si Google cambió desde la última sync (su <c>updated</c> ≠ el guardado) gana
///   Google (pull); si Google NO cambió pero el local difiere, gana el local (push).
/// - Remota nueva (sin par local) → se crea en local.
/// - Local que estaba sincronizada y ya no está en Google → se borra en local.
/// (El borrado local→Google no se propaga en V1: requiere "tombstones"; para borrar en Google,
/// hazlo desde Google.)
/// </summary>
public static class TaskSync
{
    public static TaskSyncPlan Plan(IReadOnlyList<SyncLocalTask> local, IReadOnlyList<SyncRemoteTask> remote)
    {
        var remoteById = remote.Where(r => !string.IsNullOrEmpty(r.ExternalId))
                               .GroupBy(r => r.ExternalId).ToDictionary(g => g.Key, g => g.First());
        var localExtIds = new HashSet<string>(local.Where(l => l.ExternalId is not null).Select(l => l.ExternalId!));

        var pushNew = new List<SyncLocalTask>();
        var pushUpdate = new List<SyncLocalTask>();
        var pullUpdate = new List<(string, SyncRemoteTask)>();
        var deleteLocal = new List<string>();

        foreach (var l in local)
        {
            if (l.ExternalId is null) { pushNew.Add(l); continue; }
            if (remoteById.TryGetValue(l.ExternalId, out var r))
            {
                if (r.Updated != l.ExternalUpdated)                 // Google cambió → pull
                    pullUpdate.Add((l.LocalId, r));
                else if (r.Title != l.Text || r.Done != l.Done)     // Google igual, local difiere → push
                    pushUpdate.Add(l);
            }
            else deleteLocal.Add(l.LocalId);                        // desapareció de Google → borrar local
        }

        var pullNew = remote.Where(r => !string.IsNullOrEmpty(r.ExternalId) && !localExtIds.Contains(r.ExternalId)).ToList();

        return new TaskSyncPlan
        {
            PushNew = pushNew,
            PushUpdate = pushUpdate,
            PullUpdate = pullUpdate,
            PullNew = pullNew,
            DeleteLocal = deleteLocal
        };
    }
}
