using System;
using System.Collections.Generic;
using System.Linq;

namespace Ritmo.Core.Focus;

/// <summary>
/// «Workspace» de un entorno: los enlaces que se abren de golpe en el navegador al
/// pulsar «Abrir workspace» (módulo Herramientas externas, #78). PURO y testeable;
/// el host (Studio/Flutter) resuelve el navegador y lanza el proceso.
/// </summary>
public static class EnvironmentWorkspace
{
    /// <summary>
    /// URLs a abrir para el workspace del entorno: los enlaces del entorno, normalizados
    /// (trim), sin vacíos y sin duplicados (ignorando mayúsculas/minúsculas), preservando
    /// el orden de aparición.
    /// </summary>
    public static IReadOnlyList<string> Urls(FocusEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var link in env.Links)
        {
            var url = (link.Url ?? "").Trim();
            if (url.Length == 0) continue;
            if (seen.Add(url)) result.Add(url);
        }
        return result;
    }

    /// <summary>True si el entorno tiene al menos un enlace abrible.</summary>
    public static bool CanOpen(FocusEnvironment env) => Urls(env).Count > 0;
}
