using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Ritmo.Core.Model;

namespace Ritmo_App.Services;

/// <summary>Color de fondo por tipo de bloque (coherente con el Excel del horario).</summary>
internal static class ScheduleColors
{
    // Overrides elegidos por el usuario (ViewConfig.ColorsByKind). Se refrescan antes de
    // cada render (SchedulePage.Build / DayPreviewDialog); si un tipo no está, se usa su
    // color por defecto. Estado estático = presentación global, hilo de UI único. #45
    private static IReadOnlyDictionary<StudyKind, string> _overrides =
        new Dictionary<StudyKind, string>();

    /// <summary>Aplica los colores personalizados por tipo (los que falten usan el por defecto).</summary>
    public static void SetOverrides(IReadOnlyDictionary<StudyKind, string>? overrides)
        => _overrides = overrides ?? new Dictionary<StudyKind, string>();

    private static Color Hex(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromArgb(255,
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }

    public static Brush For(StudyKind kind)
    {
        if (_overrides.TryGetValue(kind, out var custom) && custom.TrimStart('#').Length == 6)
        {
            try { return new SolidColorBrush(Hex(custom)); }
            catch { /* color guardado corrupto: cae al de por defecto */ }
        }
        return new SolidColorBrush(kind switch
        {
            StudyKind.Tecnico => Hex("#E2EFDA"),
            StudyKind.Legislacion => Hex("#DCE6F1"),
            StudyKind.Ingles => Hex("#FDE2C8"),
            StudyKind.Tests => Hex("#E4DFEC"),
            StudyKind.Simulacro => Hex("#F8CBAD"),
            StudyKind.Descanso => Hex("#FCE9D6"),
            StudyKind.Personal => Hex("#FCE4EC"),
            StudyKind.PorDefinir => Hex("#F2F2F2"),
            _ => Hex("#EDEDED")
        });
    }

    public static Brush TextFor(StudyKind kind) => new SolidColorBrush(kind switch
    {
        StudyKind.Tecnico => Hex("#548235"),
        StudyKind.Legislacion => Hex("#1F4E79"),
        StudyKind.Ingles => Hex("#C55A11"),
        StudyKind.Tests => Hex("#7030A0"),
        StudyKind.Simulacro => Hex("#C0392B"),
        StudyKind.Personal => Hex("#AD1457"),
        _ => Hex("#595959")
    });
}
