using System;
using System.Collections.Generic;

namespace Ritmo.Core.Focus;

/// <summary>
/// Bloqueo "blando" de webs distractoras durante la sesión (#8/#33). Como Ritmo es una app MSIX
/// (sandbox) y no puede editar el archivo hosts ni el firewall sin elevación, el enforcement se hace
/// dentro del sandbox: se detecta cuándo la pestaña ACTIVA de un navegador está en una web bloqueada
/// y el host minimiza esa ventana. No hay cambio persistente en el sistema, así que "restaurar al
/// terminar" es automático (basta con dejar de minimizar).
///
/// Esta función es PURA y testeable: el título de una pestaña NO contiene la URL, así que se compara
/// el título de la ventana contra la "etiqueta principal" del dominio (p. ej. «youtube» de
/// youtube.com), exigiendo ≥3 caracteres para evitar falsos positivos; también vale si el dominio
/// completo aparece literal en el título.
/// </summary>
public static class DistractionBlock
{
    /// <summary>Devuelve el dominio bloqueado que coincide con el título de ventana, o null si ninguno.</summary>
    public static string? MatchedSite(string? windowTitle, IEnumerable<string> blockedDomains)
    {
        var title = (windowTitle ?? "").ToLowerInvariant();
        if (title.Length == 0) return null;

        foreach (var raw in blockedDomains)
        {
            var dom = WebDomain.Normalize(raw);
            if (dom.Length == 0) continue;
            if (title.Contains(dom, StringComparison.Ordinal)) return dom;   // dominio literal en el título
            var label = dom.Split('.')[0];
            if (label.Length >= 3 && ContainsWord(title, label)) return dom;
        }
        return null;
    }

    /// <summary>¿El título contiene <paramref name="word"/> como palabra (sin letra/dígito a los lados)?</summary>
    private static bool ContainsWord(string haystack, string word)
    {
        int i = 0;
        while ((i = haystack.IndexOf(word, i, StringComparison.Ordinal)) >= 0)
        {
            bool leftOk = i == 0 || !char.IsLetterOrDigit(haystack[i - 1]);
            int end = i + word.Length;
            bool rightOk = end >= haystack.Length || !char.IsLetterOrDigit(haystack[end]);
            if (leftOk && rightOk) return true;
            i = end;
        }
        return false;
    }
}
