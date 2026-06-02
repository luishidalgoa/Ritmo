using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ritmo.Core.Persistence;

namespace Ritmo_App.Services;

/// <summary>
/// Auto-sincronización con DEBOUNCE (#64): cuando cambia una tarea de una lista YA vinculada a un
/// proveedor (Google/Apple), espera 15 s sin más cambios y sincroniza sola (push del cambio + pull).
/// Solo reacciona a datos SINCRONIZADOS (bloques con Provider) — editar una lista local NO dispara
/// nada (esa se sube con el botón manual, que la "reclama"). Servicio de nivel app, vivo en segundo plano.
/// </summary>
public sealed class TaskAutoSync
{
    public static TaskAutoSync Instance { get; } = new();

    private const int DebounceMs = 15_000;
    private readonly object _gate = new();
    private Timer? _timer;
    private string _lastSig = "";
    private bool _running;

    private TaskAutoSync() { }

    public void Start()
    {
        _lastSig = Signature(AppState.Load());
        AppState.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        lock (_gate)
        {
            if (_running) return;                       // ignora los guardados que provoca la propia sync
            var sig = Signature(AppState.Load());
            if (sig == _lastSig) return;                // los datos sincronizados no cambiaron
            _lastSig = sig;
            if (!AnyProviderConnected()) return;        // sin conexión: nada que sincronizar
            _timer?.Dispose();                          // (re)arranca el debounce de 15 s
            _timer = new Timer(_ => _ = FireAsync(), null, DebounceMs, Timeout.Infinite);
        }
    }

    private static bool AnyProviderConnected()
        => GoogleTasksService.HasSession || AppleRemindersService.HasSession;

    private async Task FireAsync()
    {
        lock (_gate) { if (_running) return; _running = true; }
        try
        {
            if (GoogleTasksService.HasSession) await GoogleTasksSync.SyncAsync();
            if (AppleRemindersService.HasSession) await AppleRemindersSync.SyncAsync();
        }
        catch { /* best-effort: si falla, queda el botón "Sincronizar ahora" */ }
        finally
        {
            // El estado post-sync pasa a ser la nueva línea base (su propio guardado no re-dispara).
            lock (_gate) { _lastSig = Signature(AppState.Load()); _running = false; }
        }
    }

    /// <summary>Firma SOLO de los datos sincronizados (bloques con Provider + sus tareas + nº de lápidas).</summary>
    private static string Signature(AppSettings s)
    {
        var syncedBlocks = s.TaskBlocks.Where(b => !string.IsNullOrEmpty(b.Provider)).Select(b => b.Id).ToHashSet();
        var sb = new StringBuilder();
        foreach (var b in s.TaskBlocks.Where(b => syncedBlocks.Contains(b.Id)).OrderBy(b => b.Id))
            sb.Append(b.Id).Append('~').Append(b.Name).Append('~').Append(b.ExternalId).Append(';');
        sb.Append('#');
        foreach (var t in s.Tasks.Where(t => syncedBlocks.Contains(t.BlockId)).OrderBy(t => t.Id))
            sb.Append(t.Id).Append('~').Append(t.Text).Append('~').Append(t.Done ? '1' : '0').Append('~').Append(t.ExternalId).Append(';');
        sb.Append('#').Append(s.TaskTombstones.Count);
        return sb.ToString();
    }
}
