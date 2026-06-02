using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ritmo.Core.Sync;

namespace Ritmo_App.Services;

/// <summary>Adaptador de Google Tasks al runner genérico (#64).</summary>
internal sealed class GoogleSyncProvider : ITaskSyncProvider
{
    public string ProviderName => "google";
    public string DisplayName => "Google Tasks";
    public bool HasSession => GoogleTasksService.HasSession;

    public async Task<IReadOnlyList<(string Id, string Title)>> GetListsAsync(CancellationToken ct)
        => (await GoogleTasksService.GetTaskListsAsync(ct)).Select(l => (l.Id, l.Title)).ToList();

    public Task<string?> CreateListAsync(string name, CancellationToken ct)
        => GoogleTasksService.InsertTaskListAsync(name, ct);

    public async Task<IReadOnlyList<SyncRemoteTask>> ListTasksAsync(string listId, CancellationToken ct)
        => (await GoogleTasksService.ListTasksAsync(listId, ct))
            .Select(r => new SyncRemoteTask(r.Id, r.Title, r.Done, r.Updated ?? "")).ToList();

    public async Task<(string Id, string Updated)?> CreateTaskAsync(string listId, string text, bool done, CancellationToken ct)
    {
        var g = await GoogleTasksService.InsertTaskAsync(listId, text, done, ct);
        return g is null ? null : (g.Id, g.Updated ?? "");
    }

    public async Task<string?> UpdateTaskAsync(string listId, string taskId, string text, bool done, CancellationToken ct)
    {
        var g = await GoogleTasksService.PatchTaskAsync(listId, taskId, text, done, ct);
        return g?.Updated;
    }

    public Task<bool> DeleteTaskAsync(string listId, string taskId, CancellationToken ct)
        => GoogleTasksService.DeleteTaskAsync(listId, taskId, ct);
}
