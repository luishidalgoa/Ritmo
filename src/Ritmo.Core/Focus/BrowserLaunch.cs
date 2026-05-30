using System.Collections.Generic;

namespace Ritmo.Core.Focus;

/// <summary>Familia de navegador, para saber qué argumentos de "ventana nueva" usar.</summary>
public enum BrowserFamily { Chromium, Firefox, Other }

/// <summary>
/// Lógica pura para abrir los enlaces de un entorno en una VENTANA NUEVA del
/// navegador por defecto del sistema (sea Edge, Chrome, Firefox, Brave…). El host
/// resuelve cuál es el navegador por defecto y lanza el proceso; aquí solo está
/// el parseo del comando, la detección de familia y la construcción de argumentos. #109
/// </summary>
public static class BrowserLaunch
{
    /// <summary>
    /// Extrae la ruta del ejecutable del comando registrado en
    /// <c>HKCR\&lt;ProgId&gt;\shell\open\command</c> (p. ej.
    /// <c>"C:\…\firefox.exe" -osint -url "%1"</c> → <c>C:\…\firefox.exe</c>).
    /// </summary>
    public static string? ExtractExePath(string? shellCommand)
    {
        var s = (shellCommand ?? "").Trim();
        if (s.Length == 0) return null;
        if (s[0] == '"')
        {
            var end = s.IndexOf('"', 1);
            return end > 1 ? s[1..end] : null;
        }
        var sp = s.IndexOf(' ');
        return sp < 0 ? s : s[..sp];
    }

    /// <summary>Deduce la familia a partir del nombre del ejecutable.</summary>
    public static BrowserFamily FamilyFromExe(string? exePath)
    {
        var n = System.IO.Path.GetFileNameWithoutExtension(exePath ?? "").ToLowerInvariant();
        if (n.Length == 0) return BrowserFamily.Other;
        if (n.Contains("firefox")) return BrowserFamily.Firefox;
        if (n.Contains("chrome") || n.Contains("msedge") || n == "edge"
            || n.Contains("brave") || n.Contains("opera") || n.Contains("vivaldi") || n.Contains("chromium"))
            return BrowserFamily.Chromium;
        return BrowserFamily.Other;
    }

    /// <summary>
    /// Argumentos para abrir <paramref name="urls"/> en una ventana nueva. Chromium:
    /// <c>--new-window u1 u2 …</c>. Firefox: <c>-new-window u1 -new-tab u2 …</c>.
    /// Familia desconocida → vacío (el host abre por shell, sin garantía de ventana nueva).
    /// </summary>
    public static IReadOnlyList<string> NewWindowArgs(BrowserFamily family, IReadOnlyList<string> urls)
    {
        if (urls.Count == 0) return [];
        switch (family)
        {
            case BrowserFamily.Chromium:
                return ["--new-window", .. urls];
            case BrowserFamily.Firefox:
                var args = new List<string> { "-new-window", urls[0] };
                for (int i = 1; i < urls.Count; i++) { args.Add("-new-tab"); args.Add(urls[i]); }
                return args;
            default:
                return [];
        }
    }
}
