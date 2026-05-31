using System;
using System.Collections.Generic;
using System.Linq;
using Ritmo.Core.Model;

namespace Ritmo.Core.Persistence;

/// <summary>
/// Migración de datos al modelo de categorías abiertas (#83). Se aplica al final de
/// <c>SettingsMapper.FromDto</c> (cubre cargar de disco e importar JSON). Garantiza que:
///  - Toda categoría referenciada por sesiones / oneoffs / mapeo-entorno / colores legacy
///    existe en el registro (auto-creada desde <see cref="LegacyCategories"/> o genérica gris).
///  - Los overrides de color legacy (<c>viewConfig.colorsByKind</c>) se fusionan en
///    <c>BlockCategory.ColorHex</c> (el override gana).
///  - Siempre existen las categorías de sistema «Otro» y «Por definir».
///  - Si el usuario ya tenía datos (plan o horario), se marca el onboarding como completado
///    para que NO vea el selector de plantillas.
/// </summary>
internal static class CategoryMigration
{
    public static AppSettings Apply(AppSettings s, IReadOnlyDictionary<string, string>? legacyColorOverrides)
    {
        var byId = new Dictionary<string, BlockCategory>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in s.Categories)
            if (!string.IsNullOrWhiteSpace(c.Id)) byId[c.Id] = c;

        // 1. Recolectar todos los ids de categoría referenciados.
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddFrom(IEnumerable<StudySession> xs)
        {
            foreach (var x in xs)
                if (!string.IsNullOrWhiteSpace(x.CategoryId)) referenced.Add(x.CategoryId);
        }
        AddFrom(s.Schedule.Sessions);
        foreach (var ph in s.Plan.Phases) AddFrom(ph.Schedule.Sessions);
        foreach (var o in s.OneOffSessions)
            if (!string.IsNullOrWhiteSpace(o.CategoryId)) referenced.Add(o.CategoryId);
        foreach (var key in s.EnvironmentByKind.Keys) referenced.Add(key);
        if (legacyColorOverrides is not null)
            foreach (var key in legacyColorOverrides.Keys) referenced.Add(key);

        // 2. Auto-crear las categorías referenciadas que falten.
        int nextOrder = byId.Count;
        foreach (var id in referenced)
        {
            if (byId.ContainsKey(id)) continue;
            if (LegacyCategories.ById.TryGetValue(id, out var legacy))
                byId[id] = legacy;
            else
                byId[id] = new BlockCategory
                {
                    Id = id, Name = id,
                    ColorHex = LegacyCategories.UnknownColor,
                    TextColorHex = LegacyCategories.UnknownTextColor,
                    IsFocus = false, Order = nextOrder++
                };
        }

        // 3. Fusionar overrides de color legacy (el override gana sobre el color base).
        if (legacyColorOverrides is not null)
            foreach (var (id, hex) in legacyColorOverrides)
                if (byId.TryGetValue(id, out var cat) && TryNormalizeHex(hex, out var norm))
                    byId[id] = cat with { ColorHex = norm };

        // 4. Garantizar las categorías de sistema.
        EnsureSystem(byId, CategoryIds.Undecided, "Por definir", "#F2F2F2", "#595959");
        EnsureSystem(byId, CategoryIds.Other, "Otro", "#EDEDED", "#595959");

        // 5. Orden contiguo determinista (por Order, luego nombre).
        var ordered = byId.Values
            .OrderBy(c => c.Order).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select((c, i) => c with { Order = i })
            .ToList();

        bool hasData = s.Plan.Phases.Count > 0 || s.Schedule.Sessions.Count > 0;
        return s with
        {
            Categories = ordered,
            OnboardingCompleted = s.OnboardingCompleted || hasData
        };
    }

    private static void EnsureSystem(Dictionary<string, BlockCategory> byId, string id, string name, string color, string text)
    {
        if (byId.TryGetValue(id, out var existing))
            byId[id] = existing with { IsSystem = true };   // forzar flag de sistema
        else
            byId[id] = new BlockCategory { Id = id, Name = name, ColorHex = color, TextColorHex = text, IsFocus = false, Order = byId.Count, IsSystem = true };
    }

    private static bool TryNormalizeHex(string? hex, out string normalized)
    {
        normalized = "";
        var h = (hex ?? "").Trim().TrimStart('#');
        if (h.Length != 6) return false;
        foreach (var ch in h)
            if (!Uri.IsHexDigit(ch)) return false;
        normalized = "#" + h.ToUpperInvariant();
        return true;
    }
}
