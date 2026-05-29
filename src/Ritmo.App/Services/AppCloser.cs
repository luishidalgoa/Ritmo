using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ritmo_App.Services;

/// <summary>
/// Cierra apps "de ruido" que el usuario haya configurado, al entrar en
/// concentración. Salvaguardas:
///  - Solo cierra procesos cuyo nombre esté en la lista EXPLÍCITA del usuario.
///  - Cierre AMABLE (CloseMainWindow, que respeta diálogos de guardado); nunca Kill.
///  - Nunca toca procesos críticos del sistema ni el propio Ritmo.
/// </summary>
public static class AppCloser
{
    // Procesos que jamás se cierran, aunque el usuario los liste por error.
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "csrss", "winlogon", "services", "lsass", "smss", "wininit",
        "system", "dwm", "svchost", "Ritmo.App", "dotnet", "devenv", "Code"
    };

    /// <summary>
    /// Cierra los procesos cuyos nombres aparezcan en <paramref name="appNames"/>.
    /// Devuelve cuántos procesos se pidieron cerrar. Best-effort y silencioso.
    /// </summary>
    public static int CloseAll(IEnumerable<string> appNames)
    {
        int closed = 0;
        foreach (var raw in appNames)
        {
            var name = Normalize(raw);
            if (string.IsNullOrWhiteSpace(name) || Protected.Contains(name))
                continue;

            Process[] procs;
            try { procs = Process.GetProcessesByName(name); }
            catch { continue; }

            foreach (var p in procs)
            {
                try
                {
                    // 1º intento: cierre amable (permite diálogos de guardado).
                    bool asked = p.MainWindowHandle != IntPtr.Zero && p.CloseMainWindow();

                    if (!asked)
                    {
                        // Sin ventana visible (p. ej. apps en bandeja como Discord) o no
                        // respondió: el usuario lo listó EXPLÍCITAMENTE para cerrarlo, así
                        // que cerramos el proceso. Es su decisión consciente.
                        p.Kill();
                    }
                    closed++;
                }
                catch { /* sin permisos / ya cerrado: ignorar */ }
                finally { p.Dispose(); }
            }
        }
        return closed;
    }

    /// <summary>Quita extensión .exe y espacios para comparar por nombre de proceso.</summary>
    private static string Normalize(string s)
    {
        s = s.Trim();
        if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            s = s[..^4];
        return s;
    }
}
