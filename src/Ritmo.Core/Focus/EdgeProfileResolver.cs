using System;
using System.Collections.Generic;
using System.Linq;

namespace Ritmo.Core.Focus;

/// <summary>
/// Decisión sobre qué perfil de Edge usar para estudio.
/// <paramref name="Folder"/> es la carpeta interna de Edge ("Profile N" / "Default").
/// <paramref name="DisplayName"/> es el nombre visible del perfil.
/// <paramref name="NeedsCreation"/> indica si hay que registrarlo (no existía).
/// </summary>
public sealed record EdgeProfileDecision(string Folder, string DisplayName, bool NeedsCreation);

/// <summary>
/// Lógica PURA (sin Windows, sin IO) para resolver el perfil de estudio en Edge.
/// Reutiliza un perfil existente si su nombre coincide con variantes conocidas
/// (inglés y español); si no, propone crear uno nombrado según el idioma del
/// sistema. El host (Ritmo.App) hace la lectura/escritura del Local State y lanza
/// el navegador. Así la decisión es 100% testable.
/// </summary>
public static class EdgeProfileResolver
{
    /// <summary>Variantes de nombre que delatan un perfil de concentración/estudio.</summary>
    public static readonly string[] StudyNameVariants =
        { "study", "estudio", "focus", "concentr", "deep work", "trabajo" };

    /// <summary>
    /// Resuelve el perfil de estudio.
    /// </summary>
    /// <param name="existingProfiles">folder → nombre visible, leídos del Local State.</param>
    /// <param name="systemLanguage">Código de idioma del sistema (p. ej. "es", "es-ES", "en-US").</param>
    /// <param name="reservedFolders">Carpetas extra que no se deben reutilizar como nuevas (p. ej. dirs en disco).</param>
    public static EdgeProfileDecision Resolve(
        IReadOnlyDictionary<string, string> existingProfiles,
        string systemLanguage,
        IEnumerable<string>? reservedFolders = null)
    {
        existingProfiles ??= new Dictionary<string, string>();

        // 1) Reutilizar un perfil de estudio ya existente (match por nombre).
        foreach (var kv in existingProfiles)
        {
            if (IsStudyName(kv.Value))
                return new EdgeProfileDecision(kv.Key, kv.Value, NeedsCreation: false);
        }

        // 2) No hay ninguno: proponer crear el siguiente "Profile N" libre.
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in existingProfiles.Keys) reserved.Add(k);
        if (reservedFolders is not null)
            foreach (var f in reservedFolders) reserved.Add(f);

        var folder = NextFreeFolder(reserved);
        var name = StudyNameFor(systemLanguage);
        return new EdgeProfileDecision(folder, name, NeedsCreation: true);
    }

    /// <summary>¿El nombre del perfil delata que es de estudio/concentración?</summary>
    public static bool IsStudyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var lower = name.ToLowerInvariant();
        return StudyNameVariants.Any(v => lower.Contains(v));
    }

    /// <summary>Nombre propuesto según el idioma del sistema (es → "Estudio", resto → "Study").</summary>
    public static string StudyNameFor(string? systemLanguage)
        => !string.IsNullOrEmpty(systemLanguage) &&
           systemLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase)
            ? "Estudio"
            : "Study";

    private static string NextFreeFolder(ISet<string> reserved)
    {
        for (int i = 1; i < 1000; i++)
        {
            var f = $"Profile {i}";
            if (!reserved.Contains(f)) return f;
        }
        return "Profile Estudio";
    }
}
