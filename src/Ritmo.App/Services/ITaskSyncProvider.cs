using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ritmo.Core.Sync;

namespace Ritmo_App.Services;

/// <summary>
/// Abstracción de un proveedor externo de tareas (Google Tasks, Microsoft To Do, Apple Recordatorios)
/// para el orquestador genérico <see cref="TaskSyncRunner"/> (#64). Expone "listas" (bloques) y sus
/// "tareas" en términos neutros; cada proveedor traduce a su API/protocolo. El motor de reconciliación
/// (<see cref="TaskSync"/>) y la lógica de scoping por proveedor viven en el runner, no aquí.
/// </summary>
public interface ITaskSyncProvider
{
    /// <summary>Etiqueta estable del proveedor: "google" | "microsoft" | "apple".</summary>
    string ProviderName { get; }

    /// <summary>Nombre visible para mensajes (p. ej. "Microsoft To Do").</summary>
    string DisplayName { get; }

    /// <summary>¿Hay sesión iniciada con este proveedor?</summary>
    bool HasSession { get; }

    /// <summary>Listas remotas del usuario (id externo + título).</summary>
    Task<IReadOnlyList<(string Id, string Title)>> GetListsAsync(CancellationToken ct);

    /// <summary>Crea una lista remota y devuelve su id (o null si falla).</summary>
    Task<string?> CreateListAsync(string name, CancellationToken ct);

    /// <summary>Tareas de una lista remota, ya en la forma neutra del motor de sync.</summary>
    Task<IReadOnlyList<SyncRemoteTask>> ListTasksAsync(string listId, CancellationToken ct);

    /// <summary>Crea una tarea remota; devuelve (id externo, marca de actualización) o null.</summary>
    Task<(string Id, string Updated)?> CreateTaskAsync(string listId, string text, bool done, CancellationToken ct);

    /// <summary>Actualiza una tarea remota; devuelve la nueva marca de actualización o null.</summary>
    Task<string?> UpdateTaskAsync(string listId, string taskId, string text, bool done, CancellationToken ct);

    /// <summary>
    /// Borra una tarea remota (#64). Devuelve true si quedó borrada o si ya no existía (404);
    /// false si el borrado falló y conviene reintentarlo en la próxima sincronización.
    /// </summary>
    Task<bool> DeleteTaskAsync(string listId, string taskId, CancellationToken ct);
}
