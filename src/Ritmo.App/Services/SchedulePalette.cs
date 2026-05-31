using System;
using System.Collections.Generic;

namespace Ritmo_App.Services;

/// <summary>
/// Paleta curada para los colores del horario (#45): familias de color en COLUMNAS,
/// cada una con tintes de mayor a menor intensidad (de arriba abajo). Los tintes se
/// derivan mezclando el tono base hacia blanco, así la rejilla queda limpia y cohesiva.
/// </summary>
internal static class SchedulePalette
{
    // Tonos base (intensos), uno por columna.
    private static readonly string[] Bases =
    {
        "#E53935", // rojo
        "#FB8C00", // naranja
        "#FDD835", // amarillo
        "#7CB342", // lima
        "#43A047", // verde
        "#00897B", // teal
        "#1E88E5", // azul
        "#5E35B1", // índigo
        "#8E24AA", // morado
        "#D81B60", // rosa
        "#607D8B"  // gris azulado
    };

    // Mezcla hacia blanco por fila: 0 = tono puro (más intenso), arriba; valores
    // mayores = más claro (menos intenso), abajo.
    private static readonly double[] TintSteps = { 0.0, 0.28, 0.50, 0.68, 0.82 };

    /// <summary>Columnas de color (cada una de mayor a menor intensidad), en hex "#RRGGBB".</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Columns()
    {
        var cols = new List<IReadOnlyList<string>>(Bases.Length);
        foreach (var b in Bases)
        {
            var shades = new List<string>(TintSteps.Length);
            foreach (var t in TintSteps) shades.Add(LerpToWhite(b, t));
            cols.Add(shades);
        }
        return cols;
    }

    /// <summary>Mezcla un color hacia blanco en proporción <paramref name="t"/> (0..1). Devuelve "#RRGGBB".</summary>
    private static string LerpToWhite(string hex, double t)
    {
        var h = hex.TrimStart('#');
        int r = Convert.ToInt32(h.Substring(0, 2), 16);
        int g = Convert.ToInt32(h.Substring(2, 2), 16);
        int b = Convert.ToInt32(h.Substring(4, 2), 16);
        r = (int)Math.Round(r + (255 - r) * t);
        g = (int)Math.Round(g + (255 - g) * t);
        b = (int)Math.Round(b + (255 - b) * t);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
