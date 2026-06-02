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
}

/// <summary>Adaptador de Microsoft To Do (Graph) al runner genérico (#64).</summary>
internal sealed class MicrosoftSyncProvider : ITaskSyncProvider
{
    public string ProviderName => "microsoft";
    public string DisplayName => "Microsoft To Do";
    public bool HasSession => MicrosoftTodoService.HasSession;

    public async Task<IReadOnlyList<(string Id, string Title)>> GetListsAsync(CancellationToken ct)
        => (await MicrosoftTodoService.GetTaskListsAsync(ct)).Select(l => (l.Id, l.Title)).ToList();

    public Task<string?> CreateListAsync(string name, CancellationToken ct)
        => MicrosoftTodoService.InsertTaskListAsync(name, ct);

    public async Task<IReadOnlyList<SyncRemoteTask>> ListTasksAsync(string listId, CancellationToken ct)
        => (await MicrosoftTodoService.ListTasksAsync(listId, ct))
            .Select(r => new SyncRemoteTask(r.Id, r.Title, r.Done, r.Updated ?? "")).ToList();

    public async Task<(string Id, string Updated)?> CreateTaskAsync(string listId, string text, bool done, CancellationToken ct)
    {
        var t = await MicrosoftTodoService.InsertTaskAsync(listId, text, done, ct);
        return t is null ? null : (t.Id, t.Updated ?? "");
    }

    public async Task<string?> UpdateTaskAsync(string listId, string taskId, string text, bool done, CancellationToken ct)
    {
        var t = await MicrosoftTodoService.PatchTaskAsync(listId, taskId, text, done, ct);
        return t?.Updated;
    }
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
}
