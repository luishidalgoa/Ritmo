using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ritmo.Core.Model;
using Ritmo_App.Dialogs;
using Ritmo_App.Services;

namespace Ritmo_App;

/// <summary>
/// Cuadrícula semanal del horario (solo lectura): días en columnas, horas en
/// filas, leyendo la fase activa del plan. Cada sesión se pinta coloreada por
/// su tipo, ocupando las filas proporcionales a su duración.
/// </summary>
public sealed partial class SchedulePage : Page
{
    private static readonly DayOfWeek[] Days =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
          DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
    private static readonly string[] DayNames =
        { "LUNES", "MARTES", "MIÉRCOLES", "JUEVES", "VIERNES", "SÁBADO", "DOMINGO" };

    private string? _activePhaseName;

    public SchedulePage()
    {
        InitializeComponent();
        Loaded += (_, _) => Build();
    }

    private void Build()
    {
        AppState.EnsureSeeded();
        var settings = AppState.Load();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var phase = settings.Plan.GetActivePhase(today)
                    ?? settings.Plan.OrderedPhases.FirstOrDefault();
        var schedule = phase?.Schedule ?? settings.Schedule;
        _activePhaseName = phase?.Name;
        AddBtn.IsEnabled = phase is not null;

        PhaseInfo.Text = phase is null
            ? "Sin fase configurada"
            : $"{phase.Name}  ·  {phase.ValidFrom:dd/MM/yyyy} → {(phase.ValidTo?.ToString("dd/MM/yyyy") ?? "indefinida")}";

        int startH = settings.ViewConfig.DayStart.Hour;
        int endH = settings.ViewConfig.DayEnd.Hour;
        foreach (var s in schedule.Sessions)
        {
            startH = Math.Min(startH, s.Start.Hour);
            endH = Math.Max(endH, (int)Math.Ceiling((s.Start.ToTimeSpan() + s.Duration).TotalHours));
        }
        if (endH <= startH) endH = startH + 12;

        BuildGrid(schedule, startH, endH);
    }

    private void BuildGrid(WeeklySchedule schedule, int startH, int endH)
    {
        const int slotsPerHour = 2;
        const double rowHeight = 26;
        int hours = endH - startH;
        int totalRows = hours * slotsPerHour;

        var g = GridRoot;
        g.Children.Clear();
        g.RowDefinitions.Clear();
        g.ColumnDefinitions.Clear();

        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
        for (int c = 0; c < 7; c++)
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
        for (int r = 0; r < totalRows; r++)
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowHeight) });

        var line = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
        var headerBg = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"];

        for (int c = 0; c < 7; c++)
            g.Children.Add(Cell(new TextBlock {
                Text = DayNames[c], FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }, 0, c + 1, 1, headerBg, line));

        for (int h = 0; h < hours; h++)
        {
            int rowTop = 1 + h * slotsPerHour;
            g.Children.Add(Cell(new TextBlock {
                Text = $"{startH + h:00}:00", FontSize = 11, Opacity = 0.7,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0)
            }, rowTop, 0, slotsPerHour, null, line));

            for (int c = 0; c < 7; c++)
            {
                var bg = new Border { BorderBrush = line, BorderThickness = new Thickness(0, 0, 1, 1) };
                Grid.SetRow(bg, rowTop); Grid.SetRowSpan(bg, slotsPerHour);
                Grid.SetColumn(bg, c + 1);
                g.Children.Add(bg);
            }
        }

        for (int idx = 0; idx < schedule.Sessions.Count; idx++)
        {
            var s = schedule.Sessions[idx];
            int dayCol = Array.IndexOf(Days, s.Day);
            if (dayCol < 0) continue;
            int startSlot = 1 + (int)Math.Round((s.Start.Hour - startH + s.Start.Minute / 60.0) * slotsPerHour);
            int spanSlots = Math.Max(1, (int)Math.Round(s.Duration.TotalHours * slotsPerHour));
            if (startSlot < 1) startSlot = 1;

            var content = new StackPanel
            {
                Children =
                {
                    new TextBlock {
                        Text = s.Title, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = ScheduleColors.TextFor(s.Kind), TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock {
                        Text = $"{s.Start:HH\\:mm}–{s.End:HH\\:mm}{(s.IsTentative ? "  (?)" : "")}",
                        FontSize = 10, Opacity = 0.75, Foreground = ScheduleColors.TextFor(s.Kind) }
                }
            };

            // Tarjeta clicable (Button sin chrome) para editar/borrar.
            var card = new Button
            {
                Background = ScheduleColors.For(s.Kind),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(2),
                Padding = new Thickness(6, 3, 6, 3),
                Opacity = s.IsTentative ? 0.6 : 1.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Top,
                BorderThickness = new Thickness(0),
                Tag = idx,
                Content = content
            };
            card.Click += SessionCard_Click;
            Grid.SetRow(card, startSlot); Grid.SetRowSpan(card, spanSlots);
            Grid.SetColumn(card, dayCol + 1);
            g.Children.Add(card);
        }
    }

    private void AddBtn_Click(object sender, RoutedEventArgs e) => _ = ShowAddDialog();

    private void SessionCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is int idx)
            _ = ShowEditDialog(idx);
    }

    private async Task ShowAddDialog()
    {
        if (_activePhaseName is null) return;
        var dlg = new SessionDialog { XamlRoot = this.XamlRoot };
        dlg.LoadDefaults();
        var result = await dlg.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            AppState.Config.AddSession(_activePhaseName, dlg.ToSession());
            Build();
        }
    }

    private async Task ShowEditDialog(int index)
    {
        if (_activePhaseName is null) return;
        var settings = AppState.Load();
        var phase = settings.Plan.Phases.FirstOrDefault(p => p.Name == _activePhaseName);
        if (phase is null || index < 0 || index >= phase.Schedule.Sessions.Count) return;

        var dlg = new SessionDialog
        {
            XamlRoot = this.XamlRoot,
            PrimaryButtonText = "Guardar",
            SecondaryButtonText = "Cancelar",
            CloseButtonText = "Eliminar"   // el botón de cierre actúa como "eliminar"
        };
        dlg.LoadFrom(phase.Schedule.Sessions[index]);
        var result = await dlg.ShowAsync();

        if (result == ContentDialogResult.Primary)
            AppState.Config.UpdateSession(_activePhaseName, index, dlg.ToSession());
        else if (result == ContentDialogResult.None)   // CloseButton = Eliminar
            AppState.Config.RemoveSession(_activePhaseName, index);
        else
            return; // Cancelar (Secondary)
        Build();
    }

    private static FrameworkElement Cell(FrameworkElement content, int row, int col, int rowSpan, Brush? bg, Brush line)
    {
        var b = new Border
        {
            BorderBrush = line, BorderThickness = new Thickness(0, 0, 1, 1),
            Background = bg, Child = content
        };
        Grid.SetRow(b, row); Grid.SetColumn(b, col); Grid.SetRowSpan(b, rowSpan);
        return b;
    }
}
