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

        // 2) Programas instalados (registro de desinstalación) por nombre.
        var names = InstalledDisplayNames();
        foreach (var app in KnownApps.Catalog)
            if (names.Any(n => n.Contains(app.MatchTerm)))
                installed.Add(app.ProcessName);

        return installed;
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
