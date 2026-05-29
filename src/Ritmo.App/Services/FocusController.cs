using Microsoft.Win32;

namespace Ritmo_App.Services;

/// <summary>
/// Activa/desactiva el "modo concentración" del SO (silenciar notificaciones).
/// Abstracción para poder cambiar la estrategia sin tocar la UI.
/// </summary>
public interface IFocusController
{
    /// <summary>Entra en concentración (silencia notificaciones del sistema).</summary>
    void Enter();
    /// <summary>Sale de concentración (restaura el estado anterior).</summary>
    void Exit();
    /// <summary>¿Está la concentración activa ahora mismo?</summary>
    bool IsActive { get; }
}

/// <summary>
/// Implementación para Windows: alterna la clave de registro de notificaciones
/// (HKCU\...\PushNotifications\ToastEnabled). Reversible y sin permisos de admin.
/// Guarda el valor previo para restaurarlo exactamente al salir.
/// </summary>
public sealed class WindowsFocusController : IFocusController
{
    private const string KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\PushNotifications";
    private const string ValueName = "ToastEnabled";

    private int? _previous;   // valor de ToastEnabled antes de entrar

    public bool IsActive { get; private set; }

    public void Enter()
    {
        if (IsActive) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            if (key is null) return;
            _previous = key.GetValue(ValueName) as int? ?? 1;
            key.SetValue(ValueName, 0, RegistryValueKind.DWord);   // 0 = toasts silenciados
            IsActive = true;
        }
        catch
        {
            // Si no se puede tocar el registro, no rompemos el Pomodoro.
            IsActive = false;
        }
    }

    public void Exit()
    {
        if (!IsActive) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            key?.SetValue(ValueName, _previous ?? 1, RegistryValueKind.DWord);
        }
        catch { /* best-effort */ }
        finally
        {
            IsActive = false;
            _previous = null;
        }
    }
}

/// <summary>Implementación nula (para entornos donde no se quiere tocar el SO).</summary>
public sealed class NoOpFocusController : IFocusController
{
    public bool IsActive { get; private set; }
    public void Enter() => IsActive = true;
    public void Exit() => IsActive = false;
}
