using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Ritmo.Core.Focus;

namespace Ritmo_App.Services;

/// <summary>
/// Bloqueo "blando" de webs distractoras durante la concentración (#8/#33). Recorre las ventanas
/// de navegador visibles y, si la pestaña ACTIVA (= título de la ventana) está en una web bloqueada,
/// minimiza esa ventana. Es best-effort y dentro del sandbox MSIX (sin elevación, sin tocar hosts).
/// Como solo minimiza, no hay nada que "restaurar" al terminar: basta con dejar de barrer.
/// </summary>
internal static class DistractionGuard
{
    private static readonly HashSet<string> BrowserProcs = new(StringComparer.OrdinalIgnoreCase)
    {
        "msedge", "chrome", "firefox", "brave", "opera", "vivaldi", "chromium", "iexplore", "arc"
    };

    private const int SW_MINIMIZE = 6;

    /// <summary>
    /// Barre las ventanas: minimiza las de navegador cuya pestaña activa esté en una web bloqueada.
    /// Devuelve cuántas minimizó. Llamar periódicamente mientras dura la concentración.
    /// </summary>
    public static int Sweep(IReadOnlyList<string> blockedDomains)
    {
        if (blockedDomains is null || blockedDomains.Count == 0) return 0;
        int hits = 0;
        try
        {
            EnumWindows((hWnd, _) =>
            {
                try
                {
                    if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) return true;   // oculta o ya minimizada
                    int len = GetWindowTextLength(hWnd);
                    if (len <= 0) return true;
                    var sb = new StringBuilder(len + 1);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    var title = sb.ToString();
                    if (title.Length == 0) return true;

                    GetWindowThreadProcessId(hWnd, out uint pid);
                    string proc;
                    try { using var p = Process.GetProcessById((int)pid); proc = p.ProcessName; }
                    catch { return true; }
                    if (!BrowserProcs.Contains(proc)) return true;

                    if (DistractionBlock.MatchedSite(title, blockedDomains) is not null)
                    {
                        ShowWindow(hWnd, SW_MINIMIZE);
                        hits++;
                    }
                }
                catch { /* una ventana problemática no debe romper el barrido */ }
                return true;   // seguir enumerando
            }, IntPtr.Zero);
        }
        catch { /* best-effort */ }
        return hits;
    }

    // ---- P/Invoke (user32) ----
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
