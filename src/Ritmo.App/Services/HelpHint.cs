using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Ritmo.Core.Help;

namespace Ritmo_App.Services;

/// <summary>
/// Pistas de ayuda (#93): un icono «?» con un tooltip RICO (título + descripción + ejemplo
/// destacado) tomado del glosario del núcleo, para poner junto a los títulos de los campos. Misma
/// fuente de verdad (Glossary) que la página de Ayuda. <see cref="Icon"/> = «?» suelto;
/// <see cref="Label"/> = un título con su «?» al lado; <see cref="Header"/> = cabecera de sección.
/// </summary>
public static class HelpHint
{
    /// <summary>Icono de ayuda (?) con tooltip para la clave de glosario dada.</summary>
    public static FrameworkElement Icon(string key)
    {
        var icon = new FontIcon
        {
            Glyph = "",   // Help (círculo con ?)
            FontSize = 14,
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
        ToolTipService.SetToolTip(element, BuildTip(e));
    }

    /// <summary>
    /// Una etiqueta de campo con el «?» de ayuda al lado, pensada para Header de un control
    /// (NumberBox/ComboBox/ToggleSwitch) o como título de campo.
    /// </summary>
    public static FrameworkElement Label(string text, string key,
        double fontSize = 0, double opacity = 1, bool semibold = false)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        if (fontSize > 0) label.FontSize = fontSize;
        if (opacity < 1) label.Opacity = opacity;
        if (semibold) label.FontWeight = FontWeights.SemiBold;

        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(label);
        if (Glossary.Find(key) is not null) panel.Children.Add(Icon(key));
        return panel;
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

    /// <summary>Construye el contenido formateado del tooltip de un término (reutilizable).</summary>
    public static ToolTip BuildTip(GlossaryEntry entry)
    {
        var panel = new StackPanel { Spacing = 7, MaxWidth = 360 };
        panel.Children.Add(new TextBlock
        {
            Text = entry.Term, FontWeight = FontWeights.SemiBold, FontSize = 14, TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = entry.Description, FontSize = 13, Opacity = 0.9, TextWrapping = TextWrapping.Wrap, LineHeight = 18
        });
        if (!string.IsNullOrWhiteSpace(entry.Example))
        {
            var ex = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 16 };
            ex.Inlines.Add(new Run
            {
                Text = "Ejemplo:  ", FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
            });
            ex.Inlines.Add(new Run { Text = entry.Example, FontStyle = Windows.UI.Text.FontStyle.Italic });
            panel.Children.Add(ex);
        }
        return new ToolTip { Content = panel, Padding = new Thickness(12, 10, 12, 10) };
    }
}
