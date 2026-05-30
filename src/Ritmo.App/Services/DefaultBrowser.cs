using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;
using Ritmo.Core.Focus;

namespace Ritmo_App.Services;

/// <summary>
/// Abre URLs en una VENTANA NUEVA del navegador por defecto del sistema (Edge,
/// Chrome, Firefox, Brave…). La decisión de argumentos vive en el núcleo puro
/// (<see cref="BrowserLaunch"/>); aquí solo el IO: leer el navegador por defecto
/// del registro y lanzar el proceso. Best-effort: si algo falla, no rompe nada. #109
/// </summary>
public static class DefaultBrowser
{
    /// <summary>Abre los enlaces dados en una ventana nueva del navegador por defecto.</summary>
    public static void OpenLinksInNewWindow(IReadOnlyList<string> urls)
    {
        var list = urls.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToList();
        if (list.Count == 0) return;

        var exe = ResolveExe();
        var family = BrowserLaunch.FamilyFromExe(exe);
        if (exe is null || family == BrowserFamily.Other) { ShellOpenEach(list); return; }

        var args = BrowserLaunch.NewWindowArgs(family, list);
        if (args.Count == 0) { ShellOpenEach(list); return; }

        try
        {
            var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = false };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi);
        }
        catch { ShellOpenEach(list); }
    }

    /// <summary>Resuelve el ejecutable del navegador por defecto (https UserChoice → command).</summary>
    private static string? ResolveExe()
    {
        try
        {
            string? progId;
            using (var k = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice"))
                progId = k?.GetValue("ProgId") as string;
            if (string.IsNullOrEmpty(progId)) return null;

            using var cmd = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
            return BrowserLaunch.ExtractExePath(cmd?.GetValue(null) as string);
        }
        catch { return null; }
    }

    /// <summary>Reserva: abre cada URL por shell (navegador por defecto, sin garantía de ventana nueva).</summary>
    private static void ShellOpenEach(IEnumerable<string> urls)
    {
        foreach (var u in urls)
            try { Process.Start(new ProcessStartInfo { FileName = u, UseShellExecute = true }); } catch { }
    }
}
