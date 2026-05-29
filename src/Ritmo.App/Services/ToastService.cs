using System;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Ritmo.Core.Notifications;

namespace Ritmo_App.Services;

/// <summary>
/// Muestra toasts de Windows (App Notifications) usando el AppNotificationManager
/// del Windows App SDK. La app es MSIX empaquetada, así que tiene identidad de
/// paquete y el registro es válido. El QUÉ se dice lo decide el núcleo
/// (<see cref="NotificationMessage"/>); aquí solo se pinta. Best-effort.
/// </summary>
public static class ToastService
{
    private static bool _registered;
    private static readonly object _lock = new();

    /// <summary>Registra el canal de notificaciones una sola vez (idempotente).</summary>
    public static void EnsureRegistered()
    {
        if (_registered) return;
        lock (_lock)
        {
            if (_registered) return;
            try
            {
                AppNotificationManager.Default.Register();
                _registered = true;
            }
            catch { /* sin identidad o ya registrado: tolerar */ }
        }
    }

    /// <summary>Cierra el canal (al salir de la app). Best-effort.</summary>
    public static void Unregister()
    {
        try { AppNotificationManager.Default.Unregister(); } catch { }
        _registered = false;
    }

    /// <summary>Muestra el mensaje como toast. El Tag evita apilar duplicados.</summary>
    public static void Show(NotificationMessage msg)
    {
        try
        {
            EnsureRegistered();
            var toast = new AppNotificationBuilder()
                .AddText(msg.Title)
                .AddText(msg.Body)
                .BuildNotification();

            toast.Tag = msg.Tag;
            toast.Group = "ritmo";

            AppNotificationManager.Default.Show(toast);
        }
        catch { /* best-effort: nunca romper el flujo por un toast */ }
    }
}
