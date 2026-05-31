using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ritmo.Core.Model;

/// <summary>Genera ids estables (slug) para las categorías nuevas a partir de su nombre. #83</summary>
public static class CategorySlug
{
    /// <summary>
    /// Slug en minúsculas, sin acentos, con guiones (p. ej. "Lectura crítica" → "lectura-critica").
    /// Si <paramref name="existingIds"/> ya contiene el slug, añade sufijo "-2", "-3"… para evitar colisión.
    /// Si el nombre no produce nada usable, cae a "categoria".
    /// </summary>
    public static string From(string? name, IEnumerable<string>? existingIds = null)
    {
        var folded = Fold(name ?? "");
        var sb = new StringBuilder();
        bool lastDash = false;
        foreach (var ch in folded)
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(char.ToLowerInvariant(ch)); lastDash = false; }
            else if (!lastDash && sb.Length > 0) { sb.Append('-'); lastDash = true; }
        }
        var baseSlug = sb.ToString().Trim('-');
        if (baseSlug.Length == 0) baseSlug = "categoria";

        var taken = existingIds is null
            ? new HashSet<string>()
            : new HashSet<string>(existingIds, System.StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(baseSlug)) return baseSlug;
        for (int i = 2; ; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    /// <summary>Quita los diacríticos (á→a, ñ→n…) preservando las letras base.</summary>
    private static string Fold(string s)
    {
        var normalized = s.Normalize(NormalizationForm.FormD);
        var chars = normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return new string(chars.ToArray()).Normalize(NormalizationForm.FormC);
    }
}
