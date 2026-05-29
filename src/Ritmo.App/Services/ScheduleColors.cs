using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Ritmo.Core.Model;

namespace Ritmo_App.Services;

/// <summary>Color de fondo por tipo de bloque (coherente con el Excel del horario).</summary>
internal static class ScheduleColors
{
    private static Color Hex(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromArgb(255,
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }

    public static Brush For(StudyKind kind) => new SolidColorBrush(kind switch
    {
        StudyKind.Tecnico => Hex("#E2EFDA"),
        StudyKind.Legislacion => Hex("#DCE6F1"),
        StudyKind.Ingles => Hex("#FDE2C8"),
        StudyKind.Tests => Hex("#E4DFEC"),
        StudyKind.Simulacro => Hex("#F8CBAD"),
        StudyKind.Descanso => Hex("#FCE9D6"),
        StudyKind.PorDefinir => Hex("#F2F2F2"),
        _ => Hex("#EDEDED")
    });

    public static Brush TextFor(StudyKind kind) => new SolidColorBrush(kind switch
    {
        StudyKind.Tecnico => Hex("#548235"),
        StudyKind.Legislacion => Hex("#1F4E79"),
        StudyKind.Ingles => Hex("#C55A11"),
        StudyKind.Tests => Hex("#7030A0"),
        StudyKind.Simulacro => Hex("#C0392B"),
        _ => Hex("#595959")
    });
}
