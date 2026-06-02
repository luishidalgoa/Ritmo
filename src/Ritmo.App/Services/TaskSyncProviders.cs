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

/// <summary>
/// Adaptador de Recordatorios de Apple (iCloud CalDAV) al runner genérico (#64). Las "listas" son
/// colecciones VTODO de iCloud; sus ids son URLs. NOTA V1: <see cref="CreateListAsync"/> devuelve null
/// (no se crean listas nuevas en iCloud por su fragilidad con MKCALENDAR): el usuario crea la lista en
/// Recordatorios y Ritmo la sincroniza. Las TAREAS sí van en ambos sentidos dentro de listas existentes.
/// </summary>
internal sealed class AppleSyncProvider : ITaskSyncProvider
{
    public string ProviderName => "apple";
    public string DisplayName => "Recordatorios de Apple";
    public bool HasSession => AppleRemindersService.HasSession;

    public async Task<IReadOnlyList<(string Id, string Title)>> GetListsAsync(CancellationToken ct)
        => (await AppleRemindersService.GetReminderListsAsync(ct)).Select(l => (l.Url, l.Title)).ToList();

    public Task<string?> CreateListAsync(string name, CancellationToken ct) => Task.FromResult<string?>(null);

    public async Task<IReadOnlyList<SyncRemoteTask>> ListTasksAsync(string listId, CancellationToken ct)
        => (await AppleRemindersService.ListTodosAsync(listId, ct))
            .Select(r => new SyncRemoteTask(r.Url, r.Title, r.Done, r.Etag)).ToList();

    public async Task<(string Id, string Updated)?> CreateTaskAsync(string listId, string text, bool done, CancellationToken ct)
        => await AppleRemindersService.CreateTodoAsync(listId, text, done, ct);

    public async Task<string?> UpdateTaskAsync(string listId, string taskId, string text, bool done, CancellationToken ct)
        => await AppleRemindersService.UpdateTodoAsync(taskId, text, done, ct);

    public Task<bool> DeleteTaskAsync(string listId, string taskId, CancellationToken ct)
        => AppleRemindersService.DeleteTodoAsync(taskId, ct);   // en Apple el taskId ES la URL del recurso
}
