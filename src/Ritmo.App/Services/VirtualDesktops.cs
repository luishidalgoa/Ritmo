using System;
using System.Runtime.InteropServices;

namespace Ritmo_App.Services;

/// <summary>
/// Escritorios virtuales de Windows, best-effort (#110). La API COM real es no
/// documentada y cambia entre builds; por estabilidad usamos los atajos globales
/// del SO vía SendInput: <c>Win+Ctrl+D</c> (crear y cambiar a uno nuevo) y
/// <c>Win+Ctrl+F4</c> (cerrar el actual y volver).
/// </summary>
public static class VirtualDesktops
{
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_D = 0x44;
    private const ushort VK_F4 = 0x73;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>Crea un escritorio virtual nuevo y cambia a él (Win+Ctrl+D).</summary>
    public static void CreateAndSwitch() => Combo(VK_D);

    /// <summary>Cierra el escritorio virtual actual y vuelve al anterior (Win+Ctrl+F4).</summary>
    public static void CloseCurrent() => Combo(VK_F4);

    private static void Combo(ushort key)
    {
        try
        {
            var inputs = new[]
            {
                Key(VK_LWIN, false), Key(VK_CONTROL, false), Key(key, false),
                Key(key, true),      Key(VK_CONTROL, true),  Key(VK_LWIN, true),
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }
        catch { /* best-effort */ }
    }

    private static INPUT Key(ushort vk, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = keyUp ? KEYEVENTF_KEYUP : 0 } }
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion U; }

    // La unión debe tener el tamaño del miembro mayor (MOUSEINPUT) o SendInput
    // rechaza la entrada por cbSize incorrecto.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }
}
