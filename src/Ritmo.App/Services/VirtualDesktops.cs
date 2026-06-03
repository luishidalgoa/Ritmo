using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Ritmo_App.Services;

/// <summary>
/// Escritorios virtuales de Windows, best-effort (#110). La API COM real es no documentada y cambia
/// entre builds; por estabilidad combinamos:
///  • atajos globales vía SendInput (<c>Win+Ctrl+D</c> crear+cambiar, <c>Win+Ctrl+←/→</c> moverse), y
///  • LECTURA del registro de escritorios virtuales (orden, escritorio actual y nombres).
///
/// #110b: en vez de crear un escritorio NUEVO por cada concentración (se acumulaban), Ritmo crea UNO
/// llamado «Ritmo» y lo REUTILIZA: si ya existe (por nombre o por su GUID recordado), cambia a él en
/// lugar de crear otro. Al terminar vuelve al escritorio de origen, sin cerrar el de «Ritmo».
/// </summary>
public static class VirtualDesktops
{
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_D = 0x44;
    private const ushort VK_F4 = 0x73;
    private const ushort VK_LEFT = 0x25;
    private const ushort VK_RIGHT = 0x27;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const string RitmoName = "Ritmo";
    private const string VdKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

    /// <summary>Crea un escritorio virtual nuevo y cambia a él (Win+Ctrl+D).</summary>
    public static void CreateAndSwitch() => Combo(VK_D);

    /// <summary>Cierra el escritorio virtual actual y vuelve al anterior (Win+Ctrl+F4).</summary>
    public static void CloseCurrent() => Combo(VK_F4);

    // ---------- Escritorio «Ritmo» reutilizable (#110b) ----------

    /// <summary>
    /// Entra en el escritorio de concentración «Ritmo»: si ya existe (por nombre o GUID recordado),
    /// cambia a él; si no, crea uno y lo nombra. Devuelve el GUID del escritorio de ORIGEN para volver
    /// a él al terminar (null si no hay a dónde volver / no se pudo determinar). Todo best-effort.
    /// </summary>
    public static Guid? EnterRitmoDesktop()
    {
        try
        {
            var ids = GetDesktopIds();
            var current = GetCurrentId();
            if (ids.Count == 0 || current is null)   // sin registro fiable: comportamiento básico de antes
            {
                CreateAndSwitch();
                return null;
            }

            // ¿Existe ya el escritorio «Ritmo»? (por nombre, o por el GUID recordado y aún presente)
            Guid? ritmo = null;
            var named = ids.FirstOrDefault(id => string.Equals(GetName(id), RitmoName, StringComparison.OrdinalIgnoreCase));
            if (named != Guid.Empty) ritmo = named;
            if (ritmo is null && LoadSavedId() is Guid saved && ids.Contains(saved)) ritmo = saved;

            if (ritmo is Guid existing)
            {
                if (existing != current.Value) SwitchTo(existing, ids, current.Value);
                TrySetName(existing, RitmoName);   // re-asegura el nombre (cosmético)
                SaveId(existing);
                return existing == current.Value ? null : current;   // si ya estábamos en él, nada que volver
            }

            // No existe: crear uno nuevo y nombrarlo «Ritmo».
            var before = new HashSet<Guid>(ids);
            CreateAndSwitch();
            System.Threading.Thread.Sleep(350);   // que el registro refleje el escritorio nuevo
            var created = GetDesktopIds().FirstOrDefault(id => !before.Contains(id));
            if (created != Guid.Empty) { TrySetName(created, RitmoName); SaveId(created); }
            return current;
        }
        catch { return null; }
    }

    /// <summary>Vuelve al escritorio de origen al terminar la concentración. No cierra «Ritmo».</summary>
    public static void LeaveRitmoDesktop(Guid? returnTo)
    {
        try
        {
            if (returnTo is not Guid target) return;
            var ids = GetDesktopIds();
            var current = GetCurrentId();
            if (current is Guid c && ids.Contains(target)) SwitchTo(target, ids, c);
        }
        catch { }
    }

    /// <summary>Cambia al escritorio indicado moviéndose por índice con Win+Ctrl+←/→.</summary>
    private static void SwitchTo(Guid target, List<Guid> ids, Guid current)
    {
        int ci = ids.IndexOf(current), ti = ids.IndexOf(target);
        if (ci < 0 || ti < 0 || ci == ti) return;
        int steps = Math.Abs(ti - ci);
        ushort key = ti > ci ? VK_RIGHT : VK_LEFT;
        for (int i = 0; i < steps; i++)
        {
            Combo(key);
            System.Threading.Thread.Sleep(220);   // dar tiempo a la animación del SO entre saltos
        }
    }

    // ---------- Lectura/escritura del registro de escritorios virtuales ----------

    private static List<Guid> GetDesktopIds()
    {
        var list = new List<Guid>();
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(VdKey);
            if (k?.GetValue("VirtualDesktopIDs") is byte[] b)
                for (int i = 0; i + 16 <= b.Length; i += 16)
                    list.Add(new Guid(b.AsSpan(i, 16).ToArray()));
        }
        catch { }
        return list;
    }

    private static Guid? GetCurrentId()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(VdKey);
            if (k?.GetValue("CurrentVirtualDesktop") is byte[] b && b.Length == 16)
                return new Guid(b);
        }
        catch { }
        return null;
    }

    private static string? GetName(Guid id)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey($@"{VdKey}\Desktops\{id:B}");
            return k?.GetValue("Name") as string;
        }
        catch { return null; }
    }

    private static void TrySetName(Guid id, string name)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey($@"{VdKey}\Desktops\{id:B}");
            k?.SetValue("Name", name, RegistryValueKind.String);
        }
        catch { }
    }

    // GUID del escritorio «Ritmo» recordado entre arranques (respaldo si el nombre del registro no
    // sobrevive a la virtualización del paquete MSIX). Vive junto a settings.json, ruta no virtualizada.
    private static string StatePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ritmo", "focus-desktop.txt");

    private static Guid? LoadSavedId()
    {
        try { return Guid.TryParse(File.ReadAllText(StatePath()).Trim(), out var g) ? g : null; }
        catch { return null; }
    }

    private static void SaveId(Guid id)
    {
        try
        {
            var p = StatePath();
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, id.ToString());
        }
        catch { }
    }

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
