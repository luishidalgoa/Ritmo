using System;
using System.Runtime.InteropServices;

namespace Ritmo_App.Services;

/// <summary>
/// Reduce los distractores VISUALES de la barra de tareas durante la concentración (#31): suprime el
/// PARPADEO (flash naranja) del botón de Ritmo y limpia su distintivo de overlay. Al terminar, lo
/// restaura. Usa <c>ITaskbarList3</c> de Windows vía interop — sin dependencias externas.
///
/// Alcance honesto: Windows NO expone una API pública para silenciar los parpadeos de OTRAS apps
/// (eso lo gobierna Focus Assist, que es de solo lectura por programación). Por eso esto actúa sobre
/// la PROPIA ventana de Ritmo: que no robe atención con flashes/badges mientras te concentras. El
/// silenciado global de notificaciones lo cubre el módulo «No molestar» del entorno (#30).
///
/// Best-effort y silencioso: si el interop falla, no rompe el Pomodoro.
/// </summary>
public static class TaskbarSilencer
{
    private static bool _active;

    /// <summary>Suprime el parpadeo/badge del botón de la ventana <paramref name="hwnd"/>. #31</summary>
    public static void Suppress(IntPtr hwnd)
    {
        if (_active || hwnd == IntPtr.Zero) return;
        _active = true;
        try
        {
            // Detiene cualquier flasheo en curso y NO vuelve a parpadear hasta que se reactive.
            var fi = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                hwnd = hwnd,
                dwFlags = FLASHW_STOP,
                uCount = 0,
                dwTimeout = 0
            };
            FlashWindowEx(ref fi);

            // Quita el distintivo de overlay de la barra de tareas, si lo hubiera.
            TryGetTaskbarList()?.SetOverlayIcon(hwnd, IntPtr.Zero, null);
        }
        catch { /* best-effort */ }
    }

    /// <summary>Restaura el comportamiento normal (al salir de la concentración). #31</summary>
    public static void Restore()
    {
        _active = false;
        // No hay que "reactivar" nada: dejar de suprimir basta. El SO vuelve a poder flashear.
    }

    /// <summary>¿Estamos suprimiendo los distractores ahora mismo?</summary>
    public static bool IsActive => _active;

    // ---------- interop ----------

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_STOP = 0;

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    private static ITaskbarList3? TryGetTaskbarList()
    {
        try
        {
            var list = (ITaskbarList3)new TaskbarListClass();
            list.HrInit();
            return list;
        }
        catch { return null; }
    }

    [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
    private class TaskbarListClass { }

    [ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        // ITaskbarList
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        // ITaskbarList2
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
        // ITaskbarList3 (solo declaramos hasta SetOverlayIcon, lo único que usamos)
        void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
        void SetProgressState(IntPtr hwnd, int tbpFlags);
        void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
        void UnregisterTab(IntPtr hwndTab);
        void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
        void SetTabActive(IntPtr hwndTab, IntPtr hwndMDI, uint dwReserved);
        void ThumbBarAddButtons(IntPtr hwnd, uint cButtons, IntPtr pButton);
        void ThumbBarUpdateButtons(IntPtr hwnd, uint cButtons, IntPtr pButton);
        void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);
        void SetOverlayIcon(IntPtr hwnd, IntPtr hIcon, [MarshalAs(UnmanagedType.LPWStr)] string? pszDescription);
    }
}
