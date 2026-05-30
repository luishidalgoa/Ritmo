using System.Collections.Generic;
using System.Diagnostics;
using Ritmo.Core.Focus;

namespace Ritmo_App.Services;

/// <summary>
/// Abre las apps "herramienta" del entorno al concentrarse (#109). Para cada
/// nombre de proceso busca su objetivo de lanzamiento en el catálogo
/// (<see cref="KnownApps"/>): un protocolo (p. ej. <c>onenote:</c>) o un nombre
/// registrado en App Paths (p. ej. <c>winword</c>). Best-effort: si una app no
/// está o falla, no rompe la concentración.
/// </summary>
public static class AppLauncher
{
    public static void OpenAll(IReadOnlyList<string> processNames)
    {
        foreach (var p in processNames)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            var app = KnownApps.ByProcess(p);
            var target = string.IsNullOrEmpty(app?.LaunchTarget) ? p : app!.LaunchTarget;
            try { Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true }); }
            catch { /* no instalada / sin protocolo: ignorar */ }
        }
    }
}
