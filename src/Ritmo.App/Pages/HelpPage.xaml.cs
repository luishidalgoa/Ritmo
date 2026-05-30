using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ritmo.Core.Help;

namespace Ritmo_App;

/// <summary>Página de Ayuda/wiki: lista el glosario de conceptos de Ritmo (#93).</summary>
public sealed partial class HelpPage : Page
{
    public HelpPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Build();
    }

    private void Build()
    {
        EntriesList.Children.Clear();
        foreach (var e in Glossary.Entries)
        {
            var stack = new StackPanel { Spacing = 4 };
            stack.Children.Add(new TextBlock { Text = e.Term, FontSize = 16, FontWeight = FontWeights.SemiBold });
            stack.Children.Add(new TextBlock { Text = e.Description, Opacity = 0.8, TextWrapping = TextWrapping.Wrap });

            EntriesList.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Child = stack
            });
        }
    }
}
