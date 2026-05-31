using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;
using Ritmo_App.Services;

namespace Ritmo_App.Dialogs;

/// <summary>
/// Vista previa del día al iniciar concentración (#47): un resumen de los bloques de
/// HOY (horario de la fase activa + sesiones provisionales), ordenados por hora y con
/// el bloque actual resaltado. Solo lectura: el usuario lo ojea y pulsa «Empezar».
/// </summary>
public sealed partial class DayPreviewDialog : ContentDialog
{
    public DayPreviewDialog(AppSettings settings, DateTime now)
    {
        InitializeComponent();

        var day = DateOnly.FromDateTime(now);
        var es = new System.Globalization.CultureInfo("es-ES");
        DateText.Text = Capitalize(day.ToString("dddd d 'de' MMMM", es));

        var phase = settings.Plan.GetActivePhase(day) ?? settings.Plan.OrderedPhases.FirstOrDefault();
        var schedule = phase?.Schedule ?? settings.Schedule;

        var rows = schedule.Sessions
            .Where(s => s.Day == day.DayOfWeek)
            .Select(s => new Row(s.Start, s.End, s.Title, s.Kind, s.IsTentative, false))
            .Concat(settings.OneOffSessions
                .Where(o => o.Date == day)
                .Select(o => new Row(o.Start, o.Start.Add(o.Duration), o.Title, o.Kind, o.IsTentative, true)))
            .OrderBy(r => r.Start)
            .ToList();

        if (rows.Count == 0)
        {
            ListHost.Children.Add(new TextBlock
            {
                Text = "Hoy no tienes bloques programados. ¡Concentración libre!",
                Opacity = 0.7, FontSize = 13, TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        var nowT = TimeOnly.FromDateTime(now);
        foreach (var r in rows)
        {
            bool isNow = nowT >= r.Start && nowT < r.End;
            ListHost.Children.Add(BuildRow(r, isNow));
        }
    }

    private sealed record Row(TimeOnly Start, TimeOnly End, string Title, StudyKind Kind, bool Tentative, bool OneOff);

    private static UIElement BuildRow(Row r, bool isNow)
    {
        var accent = ((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color;

        var bar = new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = ScheduleColors.For(r.Kind) };

        var title = (r.Tentative ? "◇ " : "") + r.Title + (r.OneOff ? "  ✦" : "");
        var meta = $"{r.Start:HH\\:mm}–{r.End:HH\\:mm} · {r.Kind.Label()}" + (isNow ? "  ·  ahora" : "");
        var texts = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14, Opacity = r.Tentative ? 0.7 : 1.0 });
        texts.Children.Add(new TextBlock { Text = meta, Opacity = 0.7, FontSize = 12 });

        var grid = new Grid { ColumnSpacing = 10, Padding = new Thickness(10, 8, 10, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(bar, 0); Grid.SetColumn(texts, 1);
        grid.Children.Add(bar); grid.Children.Add(texts);

        return new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = isNow ? new SolidColorBrush(accent) : (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(isNow ? 2 : 1),
            CornerRadius = new CornerRadius(8),
            Child = grid
        };
    }

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
}
