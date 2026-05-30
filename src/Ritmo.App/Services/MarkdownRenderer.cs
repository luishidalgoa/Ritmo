using System;
using System.Collections.Generic;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Ritmo.Core.Text;

namespace Ritmo_App.Services;

/// <summary>
/// Convierte el Markdown sencillo del núcleo (<see cref="MarkdownLite"/>) en un
/// RichTextBlock de WinUI. El parseo (qué es negrita/enlace/viñeta) vive en el
/// núcleo y está testeado; aquí solo se pinta. #72
/// </summary>
public static class MarkdownRenderer
{
    public static RichTextBlock Build(string? markdown)
    {
        var rtb = new RichTextBlock { TextWrapping = TextWrapping.Wrap };
        foreach (var block in MarkdownLite.Parse(markdown))
        {
            var p = new Paragraph();
            switch (block.Kind)
            {
                case MdBlockKind.Heading:
                    p.Margin = new Thickness(0, 6, 0, 2);
                    AddInlines(p, block.Inlines, block.Level);
                    break;
                case MdBlockKind.Bullet:
                    p.Margin = new Thickness(12, 1, 0, 1);
                    p.Inlines.Add(new Run { Text = "•  " });
                    AddInlines(p, block.Inlines, 0);
                    break;
                case MdBlockKind.Task:
                    p.Margin = new Thickness(12, 1, 0, 1);
                    var box = new Run { Text = block.Checked ? "☑  " : "☐  ", FontFamily = new FontFamily("Segoe UI Symbol") };
                    if (block.Checked) box.Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
                    p.Inlines.Add(box);
                    AddInlines(p, block.Inlines, 0);
                    break;
                case MdBlockKind.Numbered:
                    p.Margin = new Thickness(12, 1, 0, 1);
                    p.Inlines.Add(new Run { Text = $"{block.Level}.  " });
                    AddInlines(p, block.Inlines, 0);
                    break;
                default:
                    p.Margin = new Thickness(0, 1, 0, 1);
                    AddInlines(p, block.Inlines, 0);
                    break;
            }
            rtb.Blocks.Add(p);
        }
        return rtb;
    }

    private static void AddInlines(Paragraph p, IReadOnlyList<MdInline> inlines, int headingLevel)
    {
        foreach (var i in inlines)
        {
            if (i.Href is not null)
            {
                var uri = TryUri(i.Href);
                if (uri is not null)
                {
                    var link = new Hyperlink { NavigateUri = uri };
                    link.Inlines.Add(new Run { Text = i.Text });
                    p.Inlines.Add(link);
                    continue;
                }
            }

            var run = new Run { Text = i.Text };
            if (i.Bold || headingLevel > 0) run.FontWeight = FontWeights.SemiBold;
            if (i.Italic) run.FontStyle = Windows.UI.Text.FontStyle.Italic;
            if (i.Code) run.FontFamily = new FontFamily("Consolas");
            run.FontSize = headingLevel switch { 1 => 18, 2 => 16, 3 => 14, _ => run.FontSize };
            p.Inlines.Add(run);
        }
    }

    private static Uri? TryUri(string s)
    {
        try { return new Uri(s); } catch { return null; }
    }
}
