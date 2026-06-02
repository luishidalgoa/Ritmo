using System.Threading;
using System.Threading.Tasks;

namespace Ritmo_App.Services;

/// <summary>
/// Entrada de la sincronización Bloques/Tareas de Ritmo ↔ Google Tasks (#64). Desde el multi-proveedor
/// es un envoltorio fino sobre <see cref="TaskSyncRunner"/> con el adaptador de Google; toda la lógica
/// (reconciliación + scoping por proveedor) vive en el runner.
/// </summary>
internal static class GoogleTasksSync
{
    public static Task<TaskSyncRunner.SyncResult> SyncAsync(CancellationToken ct = default)
        => TaskSyncRunner.RunAsync(new GoogleSyncProvider(), ct);
}
