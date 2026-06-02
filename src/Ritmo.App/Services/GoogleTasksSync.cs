using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ritmo.Core.Model;
using Ritmo.Core.Sync;

namespace Ritmo_App.Services;

/// <summary>
/// Orquestador de la sincronización bidireccional Bloques/Tareas de Ritmo ↔ Google Tasks (#64).
/// Usa el motor PURO <see cref="TaskSync"/> para decidir y ejecuta el plan contra la API de Google,
/// guardando el estado local UNA vez al final. Política: ver <see cref="TaskSync"/>.
/// </summary>
internal static class GoogleTasksSync
{
    public sealed record SyncResult(bool Ok, int Created, int Updated, int Deleted, string? Error);

    public static async Task<SyncResult> SyncAsync(CancellationToken ct = default)
    {
        if (!GoogleTasksService.HasSession)
            return new SyncResult(false, 0, 0, 0, "No conectado a Google Tasks.");

        int created = 0, updated = 0, deleted = 0;
        try
        {
            var s = AppState.Load();
            var blocks = s.TaskBlocks.ToList();
            var tasks = s.Tasks.ToList();

            var remoteLists = await GoogleTasksService.GetTaskListsAsync(ct);
            var remoteListIds = new HashSet<string>(remoteLists.Select(l => l.Id));

            // 1. Cada bloque local → asegurar su lista de Google (crear si no tiene o si la borraron allí).
            for (int i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i];
                if (string.IsNullOrEmpty(b.ExternalId) || !remoteListIds.Contains(b.ExternalId))
                {
                    var newId = await GoogleTasksService.InsertTaskListAsync(b.Name, ct);
                    if (newId is null) continue;
                    blocks[i] = b with { ExternalId = newId };
                    remoteListIds.Add(newId);
                }
            }

            // 2. Listas de Google sin bloque local → crear el bloque (pull a nivel de lista).
            var linked = new HashSet<string>(blocks.Where(b => b.ExternalId is not null).Select(b => b.ExternalId!));
            foreach (var rl in remoteLists)
            {
                if (linked.Contains(rl.Id)) continue;
                blocks.Add(new TaskBlock
                {
                    Id = $"block-{Guid.NewGuid():N}"[..12],
                    Name = rl.Title,
                    ExternalId = rl.Id,
                    Order = blocks.Count == 0 ? 0 : blocks.Max(x => x.Order) + 1
                });
            }

            // 3. Reconciliar tareas por bloque.
            foreach (var b in blocks)
            {
                if (string.IsNullOrEmpty(b.ExternalId)) continue;
                var listId = b.ExternalId!;
                var localTasks = tasks.Where(t => t.BlockId == b.Id).ToList();
                var remoteTasks = await GoogleTasksService.ListTasksAsync(listId, ct);

                var localSync = localTasks
                    .Select(t => new SyncLocalTask(t.Id, t.ExternalId, t.Text, t.Done, t.ExternalUpdated)).ToList();
                var remoteSync = remoteTasks.Where(r => !string.IsNullOrEmpty(r.Id))
                    .Select(r => new SyncRemoteTask(r.Id, r.Title, r.Done, r.Updated ?? "")).ToList();
                var plan = TaskSync.Plan(localSync, remoteSync);

                foreach (var l in plan.PushNew)   // crear en Google
                {
                    var g = await GoogleTasksService.InsertTaskAsync(listId, l.Text, l.Done, ct);
                    if (g is null) continue;
                    int idx = tasks.FindIndex(t => t.Id == l.LocalId);
                    if (idx >= 0) { tasks[idx] = tasks[idx] with { ExternalId = g.Id, ExternalUpdated = g.Updated }; created++; }
                }
                foreach (var l in plan.PushUpdate)   // actualizar en Google
                {
                    var g = await GoogleTasksService.PatchTaskAsync(listId, l.ExternalId!, l.Text, l.Done, ct);
                    if (g is null) continue;
                    int idx = tasks.FindIndex(t => t.Id == l.LocalId);
                    if (idx >= 0) { tasks[idx] = tasks[idx] with { ExternalUpdated = g.Updated }; updated++; }
                }
                foreach (var (localId, r) in plan.PullUpdate)   // aplicar remoto a local
                {
                    int idx = tasks.FindIndex(t => t.Id == localId);
                    if (idx >= 0) { tasks[idx] = tasks[idx] with { Text = r.Title, Done = r.Done, ExternalUpdated = r.Updated }; updated++; }
                }
                int order = localTasks.Count == 0 ? 0 : localTasks.Max(t => t.Order) + 1;
                foreach (var r in plan.PullNew)   // crear en local
                {
                    tasks.Add(new TaskItem
                    {
                        Id = $"task-{Guid.NewGuid():N}"[..12], BlockId = b.Id,
                        Text = r.Title, Done = r.Done, Order = order++,
                        ExternalId = r.ExternalId, ExternalUpdated = r.Updated
                    });
                    created++;
                }
                foreach (var localId in plan.DeleteLocal)   // borradas en Google → borrar en local
                {
                    int idx = tasks.FindIndex(t => t.Id == localId);
                    if (idx >= 0) { tasks.RemoveAt(idx); deleted++; }
                }
            }

            AppState.Store.Save(s with { TaskBlocks = blocks, Tasks = tasks });
            return new SyncResult(true, created, updated, deleted, null);
        }
        catch (Exception ex)
        {
            return new SyncResult(false, created, updated, deleted, ex.Message);
        }
    }
}
