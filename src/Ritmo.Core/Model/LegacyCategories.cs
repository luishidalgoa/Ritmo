using System.Collections.Generic;

namespace Ritmo.Core.Model;

/// <summary>
/// Mapa de los 9 valores del antiguo enum <c>StudyKind</c> a su categoría equivalente,
/// para que la migración (#83) auto-cree categorías al cargar settings legacy cuyos
/// bloques referencian "Tecnico", "Legislacion", etc. Conserva nombre/color/focus actuales.
/// Los ids son los nombres del enum tal cual (PascalCase) → el JSON existente sigue casando.
/// </summary>
public static class LegacyCategories
{
    /// <summary>Definición legacy por id (nombre del enum). Order = posición en el enum original.</summary>
    public static readonly IReadOnlyDictionary<string, BlockCategory> ById = new Dictionary<string, BlockCategory>(System.StringComparer.OrdinalIgnoreCase)
    {
        ["Tecnico"]     = new() { Id = "Tecnico",     Name = "Técnico",     ColorHex = "#E2EFDA", TextColorHex = "#548235", IsFocus = true,  Order = 0 },
        ["Legislacion"] = new() { Id = "Legislacion", Name = "Legislación", ColorHex = "#DCE6F1", TextColorHex = "#1F4E79", IsFocus = true,  Order = 1 },
        ["Ingles"]      = new() { Id = "Ingles",      Name = "Inglés",      ColorHex = "#FDE2C8", TextColorHex = "#C55A11", IsFocus = true,  Order = 2 },
        ["Tests"]       = new() { Id = "Tests",       Name = "Tests",       ColorHex = "#E4DFEC", TextColorHex = "#7030A0", IsFocus = true,  Order = 3 },
        ["Simulacro"]   = new() { Id = "Simulacro",   Name = "Simulacro",   ColorHex = "#F8CBAD", TextColorHex = "#C0392B", IsFocus = true,  Order = 4 },
        ["Descanso"]    = new() { Id = "Descanso",    Name = "Descanso",    ColorHex = "#FCE9D6", TextColorHex = "#595959", IsFocus = false, Order = 5 },
        ["PorDefinir"]  = new() { Id = CategoryIds.Undecided, Name = "Por definir", ColorHex = "#F2F2F2", TextColorHex = "#595959", IsFocus = false, Order = 6, IsSystem = true },
        ["Personal"]    = new() { Id = "Personal",    Name = "Personal",    ColorHex = "#FCE4EC", TextColorHex = "#AD1457", IsFocus = false, Order = 7 },
        ["Otro"]        = new() { Id = CategoryIds.Other, Name = "Otro", ColorHex = "#EDEDED", TextColorHex = "#595959", IsFocus = false, Order = 8, IsSystem = true },
    };

    /// <summary>Color de fondo neutro para una categoría desconocida (id no legacy).</summary>
    public const string UnknownColor = "#EDEDED";
    /// <summary>Color de texto neutro para una categoría desconocida.</summary>
    public const string UnknownTextColor = "#595959";
}
