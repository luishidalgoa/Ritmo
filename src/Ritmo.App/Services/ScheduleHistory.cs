using System.Collections.Generic;
using Ritmo.Core.Persistence;

namespace Ritmo_App.Services;

/// <summary>
/// Historial de deshacer/rehacer del Horario (#136). Guarda SNAPSHOTS completos del
/// <see cref="AppSettings"/> (inmutable) antes de cada cambio; deshacer/rehacer restauran un
/// snapshot tal cual vía <see cref="AppState.Store"/>. Sencillo y robusto: como toda la edición
/// pasa por settings, restaurar el record entero revierte cualquier cambio (sesiones, one-offs…).
///
/// Pila acotada (LRU): no crece sin límite. No persiste entre arranques (es de sesión).
/// </summary>
public sealed class ScheduleHistory
{
    private const int MaxDepth = 50;
    private readonly LinkedList<AppSettings> _undo = new();
    private readonly LinkedList<AppSettings> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// Registra el estado ACTUAL como punto de retorno, JUSTO ANTES de aplicar un cambio.
    /// Invalida la pila de rehacer (nueva rama de edición).
    /// </summary>
    public void Capture(AppSettings current)
    {
        _undo.AddLast(current);
        while (_undo.Count > MaxDepth) _undo.RemoveFirst();
        _redo.Clear();
    }

    /// <summary>
    /// Deshace: devuelve el snapshot a restaurar (o null si no hay). <paramref name="current"/>
    /// se apila en rehacer para poder volver.
    /// </summary>
    public AppSettings? Undo(AppSettings current)
    {
        if (_undo.Count == 0) return null;
        var prev = _undo.Last!.Value;
        _undo.RemoveLast();
        _redo.AddLast(current);
        return prev;
    }

    /// <summary>Rehace: devuelve el snapshot a restaurar (o null). Apila el actual en deshacer.</summary>
    public AppSettings? Redo(AppSettings current)
    {
        if (_redo.Count == 0) return null;
        var next = _redo.Last!.Value;
        _redo.RemoveLast();
        _undo.AddLast(current);
        return next;
    }
}
