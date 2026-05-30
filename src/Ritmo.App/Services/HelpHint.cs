using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Help;

namespace Ritmo_App.Services;

/// <summary>
/// Pistas de ayuda: un icono de info con un tooltip que explica un concepto, tomado
/// del glosario del núcleo (#93). Para poner junto a términos del menú («Pomodoro», etc.).
/// </summary>
public static class HelpHint
{
    /// <summary>Icono de ayuda (?) con tooltip para la clave de glosario dada.</summary>
    public static FrameworkElement Icon(string key)
    {
        var icon = new SymbolIcon(Symbol.Help)
        {
            Opacity = 0.6,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Attach(icon, key);
        return icon;
    }

    /// <summary>Pone el tooltip de ayuda de una clave sobre cualquier elemento.</summary>
    public static void Attach(DependencyObject element, string key)
    {
        var e = Glossary.Find(key);
        if (e is null) return;

        var panel = new StackPanel { Spacing = 4, MaxWidth = 300 };
        panel.Children.Add(new TextBlock { Text = e.Term, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        panel.Children.Add(new TextBlock { Text = e.Description, TextWrapping = TextWrapping.Wrap, FontSize = 13, Opacity = 0.85 });
        ToolTipService.SetToolTip(element, new ToolTip { Content = panel, Padding = new Thickness(12, 8, 12, 10) });
    }

    /// <summary>Un encabezado «texto + (?)» (para cabeceras de sección o de control).</summary>
    public static StackPanel Header(string text, string key, double fontSize = 18, bool semibold = true)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        sp.Children.Add(new TextBlock
        {
            Text = text, FontSize = fontSize,
            FontWeight = semibold ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        });
        sp.Children.Add(Icon(key));
        return sp;
    }
}
