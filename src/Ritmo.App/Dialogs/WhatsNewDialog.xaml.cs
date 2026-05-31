using System.Collections.Generic;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Updates;

namespace Ritmo_App.Dialogs;

/// <summary>
/// Carrusel «Novedades» (#updates): una diapositiva por versión con sus highlights a
/// nivel usuario. Reutiliza el patrón FlipView + PipsPager de NtfyGuideDialog.
/// </summary>
public sealed partial class WhatsNewDialog : ContentDialog
{
    public WhatsNewDialog(IReadOnlyList<ReleaseNote> notes)
    {
        InitializeComponent();

        foreach (var n in notes) Flip.Items.Add(BuildSlide(n));
        Pips.NumberOfPages = notes.Count;
        if (notes.Count <= 1) Pips.Visibility = Visibility.Collapsed;
    }

    private static FlipViewItem BuildSlide(ReleaseNote n)
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 8, Padding = new Thickness(28, 8, 28, 8) };

        panel.Children.Add(new TextBlock { Text = n.Emoji, FontSize = 52, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock
        {
            Text = n.Title, FontSize = 18, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Versión " + n.Version, FontSize = 11, Opacity = 0.55,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4)
        });

        foreach (var h in n.Highlights)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = "•", FontSize = 14, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Top });
            row.Children.Add(new TextBlock { Text = h, FontSize = 13, Opacity = 0.85, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(row);
        }

        // Por si una versión trae muchos highlights, que no se corte.
        return new FlipViewItem
        {
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Disabled }
        };
    }
}
