using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;
using Ritmo.Core.Focus;

namespace Ritmo_App.Services;

/// <summary>
/// Detecta qué apps del catálogo (<see cref="KnownApps"/>) están en el sistema:
/// en ejecución (proceso vivo) o instaladas (nombre en el registro de desinstalación).
/// Devuelve el conjunto de nombres de proceso detectados. #94
/// </summary>
public static class InstalledApps
{
    public static HashSet<string> DetectInstalled()
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) En ejecución → seguro presente.
        foreach (var app in KnownApps.Catalog)
        {
            try { if (Process.GetProcessesByName(app.ProcessName).Length > 0) installed.Add(app.ProcessName); }
            catch { /* sin permisos: ignorar */ }
        }

        // 2) App Paths del registro: la forma fiable de detectar apps de escritorio
        //    como Office (Word/Excel/Outlook) o Edge, que se instalan como un solo
        //    producto y no aparecen por nombre en el registro de desinstalación. #110
        foreach (var app in KnownApps.Catalog)
            if (HasAppPath(app.ProcessName)) installed.Add(app.ProcessName);

        // 3) Programas instalados (registro de desinstalación) + paquetes de la
        //    Microsoft Store (apps UWP como WhatsApp, que NO están en el registro).
        var names = InstalledDisplayNames();
        names.AddRange(StorePackageNames());
        foreach (var app in KnownApps.Catalog)
            if (names.Any(n => n.Contains(app.MatchTerm)))
                installed.Add(app.ProcessName);

        return installed;
    }

    /// <summary>¿Hay una entrada App Paths para &lt;processName&gt;.exe? (Office, Edge, etc.)</summary>
    private static bool HasAppPath(string processName)
    {
        const string basePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\";
        var key = basePath + processName + ".exe";
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try { using var k = root.OpenSubKey(key); if (k is not null) return true; }
            catch { }
        }
        return false;
    }

    /// <summary>
    /// Nombres (en minúsculas) de los paquetes de la Store instalados para el usuario
    /// actual. Cubre apps UWP que no aparecen en el registro de desinstalación, como
    /// WhatsApp. Mejor esfuerzo: si el SO no lo permite, devuelve vacío. #97
    /// </summary>
    private static List<string> StorePackageNames()
    {
        var names = new List<string>();
        try
        {
            var pm = new Windows.Management.Deployment.PackageManager();
            foreach (var pkg in pm.FindPackagesForUser(string.Empty))
            {
                try { if (pkg.Id?.Name is { Length: > 0 } id) names.Add(id.ToLowerInvariant()); } catch { }
                try { if (pkg.DisplayName is { Length: > 0 } dn) names.Add(dn.ToLowerInvariant()); } catch { }
            }
        }
        catch { /* sin permiso de consulta de paquetes: ignorar */ }
        return names;
    }

    private static List<string> InstalledDisplayNames()
    {
        var names = new List<string>();
        (RegistryKey Root, string Path)[] roots =
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        };
        foreach (var (root, path) in roots)
        {
            try
            {
                using var key = root.OpenSubKey(path);
                if (key is null) continue;
                foreach (var sub in key.GetSubKeyNames())
                {
                    try
                    {
                        using var k = key.OpenSubKey(sub);
                        if (k?.GetValue("DisplayName") is string dn && !string.IsNullOrWhiteSpace(dn))
                            names.Add(dn.ToLowerInvariant());
                    }
                    catch { }
                }
            }
            catch { /* clave inaccesible: ignorar */ }
        }
        return names;
    }
}
