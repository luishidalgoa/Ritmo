using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ritmo.Core.Model;
using Ritmo.Core.Sync;

namespace Ritmo_App.Services;

/// <summary>
/// Orquestador GENÉRICO de la sincronización bidireccional Bloques/Tareas de Ritmo ↔ un proveedor
/// externo (#64). Usa el motor PURO <see cref="TaskSync"/> para decidir y un <see cref="ITaskSyncProvider"/>
/// para ejecutar. Política de TaskSync + scoping por proveedor: un bloque pertenece a UN proveedor
/// (<see cref="TaskBlock.Provider"/>); la sincronización de un proveedor solo toca SUS bloques y los aún
/// sin vincular (que reclama), nunca los de otro proveedor. Guarda el estado local UNA vez al final.
/// </summary>
internal static class TaskSyncRunner
{
    public sealed record SyncResult(bool Ok, int Created, int Updated, int Deleted, string? Error);

    public static async Task<SyncResult> RunAsync(ITaskSyncProvider provider, CancellationToken ct = default)
    {
        if (!provider.HasSession)
            return new SyncResult(false, 0, 0, 0, $"No conectado a {provider.DisplayName}.");

        string prov = provider.ProviderName;
        int created = 0, updated = 0, deleted = 0;
        try
        {
            var s = AppState.Load();
            var blocks = s.TaskBlocks.ToList();
            var tasks = s.Tasks.ToList();

            // Un bloque es "de este proveedor" si ya está vinculado a él, o si está sin vincular (lo reclama).
            bool Owns(TaskBlock b) => b.Provider == prov || string.IsNullOrEmpty(b.Provider);

            var remoteLists = await provider.GetListsAsync(ct);
            var remoteListIds = new HashSet<string>(remoteLists.Select(l => l.Id));

            // 1. Cada bloque propio → asegurar su lista remota (crear si no tiene o si la borraron allí).
            for (int i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i];
                if (!Owns(b)) continue;
                if (string.IsNullOrEmpty(b.ExternalId) || !remoteListIds.Contains(b.ExternalId))
                {
                    var newId = await provider.CreateListAsync(b.Name, ct);
                    if (newId is null) continue;
                    blocks[i] = b with { ExternalId = newId, Provider = prov };
                    remoteListIds.Add(newId);
                }
                else if (b.Provider != prov)
                {
                    blocks[i] = b with { Provider = prov };   // reclama el bloque sin vincular
                }
            }

            // 2. Listas remotas sin bloque local de este proveedor → crear el bloque (pull a nivel de lista).
            var linked = new HashSet<string>(blocks.Where(b => b.Provider == prov && b.ExternalId is not null).Select(b => b.ExternalId!));
            foreach (var rl in remoteLists)
            {
                if (linked.Contains(rl.Id)) continue;
                blocks.Add(new TaskBlock
                {
                    Id = $"block-{Guid.NewGuid():N}"[..12],
                    Name = rl.Title,
                    ExternalId = rl.Id,
                    Provider = prov,
                    Order = blocks.Count == 0 ? 0 : blocks.Max(x => x.Order) + 1
                });
            }

            // 3. Reconciliar tareas por bloque propio. Cada lista va en su propio try: si una falla
            // (p. ej. una colección de iCloud que no admite REPORT), se anota el aviso y se SIGUE con
            // las demás, en vez de tumbar toda la sync y perder lo ya hecho.
            var warnings = new List<string>();
            foreach (var b in blocks)
            {
                if (b.Provider != prov || string.IsNullOrEmpty(b.ExternalId)) continue;
                try
                {
                var listId = b.ExternalId!;
                var localTasks = tasks.Where(t => t.BlockId == b.Id).ToList();
                var remoteTasks = await provider.ListTasksAsync(listId, ct);

                var localSync = localTasks
                    .Select(t => new SyncLocalTask(t.Id, t.ExternalId, t.Text, t.Done, t.ExternalUpdated)).ToList();
                var remoteSync = remoteTasks.Where(r => !string.IsNullOrEmpty(r.ExternalId)).ToList();
                var plan = TaskSync.Plan(localSync, remoteSync);

                foreach (var l in plan.PushNew)
                {
                    var g = await provider.CreateTaskAsync(listId, l.Text, l.Done, ct);
                    if (g is null) continue;
                    int idx = tasks.FindIndex(t => t.Id == l.LocalId);
                    if (idx >= 0) { tasks[idx] = tasks[idx] with { ExternalId = g.Value.Id, ExternalUpdated = g.Value.Updated }; created++; }
                }
                foreach (var l in plan.PushUpdate)
                {
                    var newUpdated = await provider.UpdateTaskAsync(listId, l.ExternalId!, l.Text, l.Done, ct);
                    if (newUpdated is null) continue;
                    int idx = tasks.FindIndex(t => t.Id == l.LocalId);
                    if (idx >= 0) { tasks[idx] = tasks[idx] with { ExternalUpdated = newUpdated }; updated++; }
                }
                foreach (var (localId, r) in plan.PullUpdate)
                {
                    int idx = tasks.FindIndex(t => t.Id == localId);
                    if (idx >= 0) { tasks[idx] = tasks[idx] with { Text = r.Title, Done = r.Done, ExternalUpdated = r.Updated }; updated++; }
                }
                int order = localTasks.Count == 0 ? 0 : localTasks.Max(t => t.Order) + 1;
                foreach (var r in plan.PullNew)
                {
                    tasks.Add(new TaskItem
                    {
                        Id = $"task-{Guid.NewGuid():N}"[..12], BlockId = b.Id,
                        Text = r.Title, Done = r.Done, Order = order++,
                        ExternalId = r.ExternalId, ExternalUpdated = r.Updated
                    });
                    created++;
                }
                foreach (var localId in plan.DeleteLocal)
                {
                    int idx = tasks.FindIndex(t => t.Id == localId);
                    if (idx >= 0) { tasks.RemoveAt(idx); deleted++; }
                }
                }
                catch (Exception ex)
                {
                    warnings.Add($"«{b.Name}»: {ex.Message}");
                }
            }

            // Guarda SIEMPRE lo conseguido (bloques nuevos + tareas), aunque alguna lista diera aviso.
            AppState.Store.Save(s with { TaskBlocks = blocks, Tasks = tasks });
            var note = warnings.Count == 0 ? null
                : $"{warnings.Count} lista(s) con aviso · " + string.Join(" · ", warnings);
            return new SyncResult(true, created, updated, deleted, note);
        }
        catch (Exception ex)
        {
            return new SyncResult(false, created, updated, deleted, ex.Message);
        }
    }
}
