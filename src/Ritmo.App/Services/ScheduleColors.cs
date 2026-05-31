using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Ritmo.Core.Model;

namespace Ritmo_App.Services;

/// <summary>
/// Color de fondo/texto por categoría de bloque (#83). Lee del registro de categorías
/// del usuario (<see cref="BlockCategory"/>), que se refresca antes de cada render con
/// <see cref="SetCategories"/>. Estado estático = presentación global, hilo de UI único.
/// </summary>
internal static class ScheduleColors
{
    private static IReadOnlyDictionary<string, BlockCategory> _byId =
        new Dictionary<string, BlockCategory>(StringComparer.OrdinalIgnoreCase);

    private const string FallbackBg = "#EDEDED";
    private const string FallbackText = "#595959";

    /// <summary>Refresca el registro de categorías (llamar antes de pintar el horario).</summary>
    public static void SetCategories(IReadOnlyList<BlockCategory>? categories)
        => _byId = (categories ?? new List<BlockCategory>())
            .ToDictionary(c => c.Id, c => c, StringComparer.OrdinalIgnoreCase);

    private static Color Hex(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromArgb(255,
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }

    private static Brush Brush(string? hex, string fallback)
    {
        var value = !string.IsNullOrWhiteSpace(hex) && hex!.TrimStart('#').Length == 6 ? hex : fallback;
        try { return new SolidColorBrush(Hex(value)); }
        catch { return new SolidColorBrush(Hex(fallback)); }
    }

    /// <summary>Color de fondo de una categoría (gris si no existe).</summary>
    public static Brush For(string? categoryId)
        => Brush(_byId.TryGetValue(categoryId ?? "", out var c) ? c.ColorHex : null, FallbackBg);

    /// <summary>Color de texto de una categoría (gris si no existe o no define texto).</summary>
    public static Brush TextFor(string? categoryId)
        => Brush(_byId.TryGetValue(categoryId ?? "", out var c) ? c.TextColorHex : null, FallbackText);
}
