using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ritmo_App.Services;

/// <summary>
/// Silencia (mute) las apps "de ruido" que el usuario configure como «Silenciar» en un
/// entorno, al entrar en concentración, y las restaura al terminar. Usa la API Core Audio
/// de Windows (sesiones de audio por proceso) vía interop — sin dependencias externas. #9
///
/// Salvaguardas (mismo espíritu que <see cref="AppCloser"/>):
///  - Solo silencia procesos cuyo nombre esté en la lista EXPLÍCITA del usuario.
///  - Solo toca sesiones que NO estaban ya muteadas, y solo esas se restauran (no pisa
///    silencios que el usuario hubiera puesto a mano).
///  - Best-effort y silencioso: si Core Audio falla, no rompe el Pomodoro.
/// </summary>
public static class AppMuter
{
    // Pids cuyas sesiones hemos muteado nosotros (para restaurarlas exactamente). #9
    private static readonly HashSet<int> _mutedByUs = new();

    /// <summary>Silencia las apps cuyos nombres de proceso aparezcan en la lista.</summary>
    public static void Mute(IEnumerable<string> processNames)
    {
        var targets = ResolveTargetPids(processNames);
        if (targets.Count == 0) return;

        try
        {
            ForEachSession((pid, vol) =>
            {
                if (!targets.Contains(pid)) return;
                if (vol.GetMute(out int isMuted) != 0) return;
                if (isMuted == 1) return;                 // ya muteada: no la tocamos
                if (vol.SetMute(1, IntPtr.Zero) == 0)
                    _mutedByUs.Add(pid);
            });
        }
        catch { /* best-effort */ }
    }

    /// <summary>Restaura (desmutea) exactamente las sesiones que silenciamos nosotros.</summary>
    public static void RestoreAll()
    {
        if (_mutedByUs.Count == 0) return;
        try
        {
            ForEachSession((pid, vol) =>
            {
                if (_mutedByUs.Contains(pid))
                    vol.SetMute(0, IntPtr.Zero);
            });
        }
        catch { /* best-effort */ }
        finally { _mutedByUs.Clear(); }
    }

    /// <summary>Pids de los procesos vivos cuyo nombre está en la lista (normalizado, sin .exe).</summary>
    private static HashSet<int> ResolveTargetPids(IEnumerable<string> processNames)
    {
        var pids = new HashSet<int>();
        foreach (var raw in processNames ?? [])
        {
            var name = Normalize(raw);
            if (name.Length == 0) continue;
            Process[] procs;
            try { procs = Process.GetProcessesByName(name); } catch { continue; }
            foreach (var p in procs) { try { pids.Add(p.Id); } catch { } finally { p.Dispose(); } }
        }
        return pids;
    }

    private static string Normalize(string s)
    {
        s = (s ?? "").Trim();
        if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s[..^4];
        return s;
    }

    /// <summary>Recorre las sesiones de audio del endpoint de salida por defecto.</summary>
    private static void ForEachSession(Action<int, ISimpleAudioVolume> visit)
    {
        var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
            Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator)!)!;
        if (enumerator.GetDefaultAudioEndpoint(0 /*eRender*/, 0 /*eConsole*/, out var device) != 0 || device is null)
            return;

        var iid = IID_IAudioSessionManager2;
        if (device.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out object mgrObj) != 0 || mgrObj is null)
            return;
        var mgr = (IAudioSessionManager2)mgrObj;

        if (mgr.GetSessionEnumerator(out var sessions) != 0 || sessions is null) return;
        if (sessions.GetCount(out int count) != 0) return;

        for (int i = 0; i < count; i++)
        {
            if (sessions.GetSession(i, out object sObj) != 0 || sObj is null) continue;
            try
            {
                if (sObj is not IAudioSessionControl2 ctl || sObj is not ISimpleAudioVolume vol) continue;
                if (ctl.GetProcessId(out int pid) != 0) continue;
                visit(pid, vol);
            }
            catch { /* sesión problemática: saltar */ }
        }
    }

    private static readonly Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static Guid IID_IAudioSessionManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
}

// ==== Interop Core Audio (validado en aislado antes de portar). Orden de métodos = vtable. ====

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object iface);
}

[ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionManager2
{
    [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, int streamFlags, out IntPtr control);
    [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, int crossProcess, out IntPtr volume);
    [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator enumerator);
}

[ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionEnumerator
{
    [PreserveSig] int GetCount(out int count);
    [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
}

[ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl2
{
    [PreserveSig] int GetState(out int state);
    [PreserveSig] int GetDisplayName(out IntPtr name);
    [PreserveSig] int SetDisplayName(string n, IntPtr ctx);
    [PreserveSig] int GetIconPath(out IntPtr path);
    [PreserveSig] int SetIconPath(string p, IntPtr ctx);
    [PreserveSig] int GetGroupingParam(out Guid g);
    [PreserveSig] int SetGroupingParam(ref Guid g, IntPtr ctx);
    [PreserveSig] int RegisterAudioSessionNotification(IntPtr n);
    [PreserveSig] int UnregisterAudioSessionNotification(IntPtr n);
    [PreserveSig] int GetSessionIdentifier(out IntPtr id);
    [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
    [PreserveSig] int GetProcessId(out int pid);
    [PreserveSig] int IsSystemSoundsSession();
    [PreserveSig] int SetDuckingPreference(int optOut);
}

[ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISimpleAudioVolume
{
    [PreserveSig] int SetMasterVolume(float level, ref Guid ctx);
    [PreserveSig] int GetMasterVolume(out float level);
    [PreserveSig] int SetMute(int mute, IntPtr ctx);
    [PreserveSig] int GetMute(out int mute);
}
