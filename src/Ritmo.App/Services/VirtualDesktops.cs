using System;
using System.Runtime.InteropServices;

namespace Ritmo_App.Services;

/// <summary>
/// Escritorios virtuales de Windows, best-effort (#110). La API COM real es no
/// documentada y cambia entre builds; por estabilidad usamos los atajos globales
/// del SO vía SendInput: <c>Win+Ctrl+D</c> (crear y cambiar a uno nuevo) y
/// <c>Win+Ctrl+F4</c> (cerrar el actual y volver). Suficiente para "un escritorio
/// limpio al concentrarte" sin interop frágil.
/// </summary>
public static class VirtualDesktops
{
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_D = 0x44;
    private const ushort VK_F4 = 0x73;

    /// <summary>Crea un escritorio virtual nuevo y cambia a él (Win+Ctrl+D).</summary>
    public static void CreateAndSwitch() => Combo(VK_D);

    /// <summary>Cierra el escritorio virtual actual y vuelve al anterior (Win+Ctrl+F4).</summary>
    public static void CloseCurrent() => Combo(VK_F4);

    private static void Combo(ushort key)
    {
        try
        {
            Send(VK_LWIN, false); Send(VK_CONTROL, false); Send(key, false);
            Send(key, true); Send(VK_CONTROL, true); Send(VK_LWIN, true);
        }
        catch { /* best-effort */ }
    }

    private static void Send(ushort vk, bool keyUp)
    {
        var input = new INPUT
        {
            type = 1, // INPUT_KEYBOARD
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0
                }
            }
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion U; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
