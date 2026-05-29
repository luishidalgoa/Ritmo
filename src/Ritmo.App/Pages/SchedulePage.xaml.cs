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

        // "En vivo" (#69): hoy + ahora + bloque activo.
        var now = DateTime.Now;
        int todayCol = Array.IndexOf(Days, now.DayOfWeek);
        var activeSession = new Ritmo.Core.Scheduling.SchedulePlanner(schedule).GetActiveSession(now);
        var accentColor = ((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color;
        var todayHeaderBrush = new SolidColorBrush(accentColor) { Opacity = 0.22 };
        var todayTint = new SolidColorBrush(accentColor) { Opacity = 0.06 };

        for (int c = 0; c < 7; c++)
        {
            bool isToday = c == todayCol;
            g.Children.Add(Cell(new TextBlock {
                Text = DayNames[c], FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = isToday
                    ? new SolidColorBrush(accentColor)
                    : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
            }, 0, c + 1, 1, isToday ? todayHeaderBrush : headerBg, line));
        }

        // Mapa de ocupación por (día, hora) para saber qué celdas están vacías.
        var occupied = new bool[7, hours];
        foreach (var s in schedule.Sessions)
        {
            int dc = Array.IndexOf(Days, s.Day);
            if (dc < 0) continue;
            int h0 = s.Start.Hour - startH;
            int h1 = (int)Math.Ceiling((s.Start.ToTimeSpan() + s.Duration).TotalHours) - startH;
            for (int hh = Math.Max(0, h0); hh < Math.Min(hours, h1); hh++) occupied[dc, hh] = true;
        }

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
                var bg = new Border
                {
                    BorderBrush = line, BorderThickness = new Thickness(0, 0, 1, 1),
                    Background = c == todayCol ? todayTint : null
                };
                Grid.SetRow(bg, rowTop); Grid.SetRowSpan(bg, slotsPerHour);
                Grid.SetColumn(bg, c + 1);
                g.Children.Add(bg);

                // Celda vacía -> botón "+" sutil para añadir una sesión en ese día/hora.
                if (!occupied[c, h])
                {
                    var addCell = new Button
                    {
                        Content = new FontIcon { Glyph = "", FontSize = 13 },
                        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                        BorderThickness = new Thickness(0),
                        Opacity = 0.0,                         // invisible hasta el hover
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Margin = new Thickness(2),
                        CornerRadius = new CornerRadius(6),
                        Tag = new int[] { c, startH + h }       // día y hora de esta celda
                    };
                    addCell.PointerEntered += (o, _) => ((Button)o).Opacity = 0.6;
                    addCell.PointerExited += (o, _) => ((Button)o).Opacity = 0.0;
                    addCell.Click += AddCell_Click;
                    Grid.SetRow(addCell, rowTop); Grid.SetRowSpan(addCell, slotsPerHour);
                    Grid.SetColumn(addCell, c + 1);
                    g.Children.Add(addCell);
                }
            }
        }

        // Fusión visual (#86): sesiones idénticas en días contiguos = una tarjeta con ColumnSpan.
        foreach (var group in Ritmo.Core.Scheduling.SessionMerge.Merge(schedule.Sessions, Days))
        {
            var s = group.Representative;
            int dayCol = group.FirstDayIndex;
            if (dayCol < 0) continue;
            int startSlot = 1 + (int)Math.Round((s.Start.Hour - startH + s.Start.Minute / 60.0) * slotsPerHour);
            int spanSlots = Math.Max(1, (int)Math.Round(s.Duration.TotalHours * slotsPerHour));
            if (startSlot < 1) startSlot = 1;

            var baseColor = ScheduleColors.For(s.Kind);
            bool isActive = activeSession is not null && group.Members.Any(m => ReferenceEquals(m, activeSession));

            // Tarjeta como Border (controlamos el hover nosotros para conservar el color).
            var card = new Border
            {
                Background = baseColor,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(2),
                Padding = new Thickness(6, 3, 6, 3),
                Opacity = s.IsTentative ? 0.6 : 1.0,
                Tag = group,
                // Bloque activo ahora: anillo de acento persistente.
                BorderBrush = isActive ? new SolidColorBrush(accentColor) : null,
                BorderThickness = isActive ? new Thickness(2) : new Thickness(0),
                Child = new StackPanel
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
                }
            };
            // Hover sutil: un borde de acento; al salir, vuelve al anillo (si activo) o a nada.
            double restThickness = isActive ? 2 : 0;
            card.PointerEntered += (o, _) => {
                var b = (Border)o;
                b.BorderThickness = new Thickness(1.5);
                b.BorderBrush = ScheduleColors.TextFor(s.Kind);
            };
            card.PointerExited += (o, _) => {
                var b = (Border)o;
                b.BorderThickness = new Thickness(restThickness);
                b.BorderBrush = isActive ? new SolidColorBrush(accentColor) : null;
            };
            card.Tapped += (o, args) => { _ = ShowEditGroup((Ritmo.Core.Scheduling.SessionGroup)((Border)o).Tag); };
            Grid.SetRow(card, startSlot); Grid.SetRowSpan(card, spanSlots);
            Grid.SetColumn(card, dayCol + 1); Grid.SetColumnSpan(card, group.DaySpan);
            g.Children.Add(card);

            // Bloque activo: botón ▶ para concentrarse en él (lleva al temporizador).
            if (isActive)
            {
                var focusBtn = new Button
                {
                    Content = new FontIcon { Glyph = "", FontSize = 12 },   // Play
                    Padding = new Thickness(4), MinWidth = 0,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 4, 6, 0),
                    CornerRadius = new CornerRadius(4)
                };
                ToolTipService.SetToolTip(focusBtn, "Concentrarme en este bloque");
                focusBtn.Click += (_, _) => FocusNow();
                Grid.SetRow(focusBtn, startSlot); Grid.SetRowSpan(focusBtn, spanSlots);
                Grid.SetColumn(focusBtn, dayCol + 1); Grid.SetColumnSpan(focusBtn, group.DaySpan);
                g.Children.Add(focusBtn);
            }
        }

        // Línea de "ahora" sobre la columna de hoy, si la hora está dentro del rango visible.
        if (todayCol >= 0)
        {
            double nowHours = now.Hour + now.Minute / 60.0 - startH;
            if (nowHours >= 0 && nowHours <= hours)
            {
                double slot = nowHours * slotsPerHour;
                int rowAt = 1 + (int)Math.Floor(slot);
                double offset = (slot - Math.Floor(slot)) * rowHeight;
                var nowLine = new Border
                {
                    Height = 2, Background = new SolidColorBrush(accentColor),
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, offset, 0, 0),
                    IsHitTestVisible = false
                };
                Grid.SetRow(nowLine, rowAt); Grid.SetColumn(nowLine, todayCol + 1);
                Grid.SetRowSpan(nowLine, 1);
                g.Children.Add(nowLine);
            }
        }
    }

    /// <summary>Lleva al temporizador y arranca la concentración del bloque activo.</summary>
    private void FocusNow() => Navigator.GoToTimer(this, autoStart: true);

    private void AddCell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is int[] dh)
            _ = ShowAddDialog(Days[dh[0]], new TimeOnly(dh[1], 0));
    }

    private void AddBtn_Click(object sender, RoutedEventArgs e) => _ = ShowAddDialog();

    private async Task ShowAddDialog(DayOfWeek? day = null, TimeOnly? start = null)
    {
        if (_activePhaseName is null) return;
        var dlg = new SessionDialog { XamlRoot = this.XamlRoot };
        dlg.LoadDefaults(day, start);
        var result = await dlg.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // Crea el bloque en CADA día marcado (#81). Si no se marcó ninguno, no hace nada.
            foreach (var d in dlg.SelectedDays)
                AppState.Config.AddSession(_activePhaseName, dlg.ToSession(d));
            Build();
        }
    }

    /// <summary>¿Comparten título/tipo/horario/provisional? (para identificar el grupo fusionado).</summary>
    private static bool SameBlock(StudySession a, StudySession b)
        => a.Title.Trim() == b.Title.Trim() && a.Kind == b.Kind
           && a.Start == b.Start && a.Duration == b.Duration && a.IsTentative == b.IsTentative;

    /// <summary>
    /// Edita/borra un grupo de sesiones fusionadas (#86). Al guardar, reemplaza las
    /// sesiones del grupo por una en cada día marcado (arregla también el bug #85:
    /// marcar más días los inserta; desmarcar los quita).
    /// </summary>
    private async Task ShowEditGroup(Ritmo.Core.Scheduling.SessionGroup group)
    {
        if (_activePhaseName is null) return;
        var settings = AppState.Load();
        var phase = settings.Plan.Phases.FirstOrDefault(p => p.Name == _activePhaseName);
        if (phase is null) return;

        var rep = group.Representative;
        var groupDays = group.Members.Select(m => m.Day).ToHashSet();
        bool Belongs(StudySession x) => SameBlock(x, rep) && groupDays.Contains(x.Day);

        var dlg = new SessionDialog
        {
            XamlRoot = this.XamlRoot,
            PrimaryButtonText = "Guardar",
            SecondaryButtonText = "Cancelar",
            CloseButtonText = "Eliminar"
        };
        dlg.LoadFrom(rep);
        dlg.PreselectDays(groupDays);   // todos los días del grupo marcados

        var result = await dlg.ShowAsync();

        // Las sesiones que NO son de este grupo se conservan tal cual.
        var kept = phase.Schedule.Sessions.Where(x => !Belongs(x)).ToList();

        if (result == ContentDialogResult.Primary)
        {
            // Reemplaza el grupo por una sesión en cada día marcado.
            var rebuilt = dlg.SelectedDays.Select(d => dlg.ToSession(d));
            AppState.Config.ReplaceSessions(_activePhaseName, [.. kept, .. rebuilt]);
        }
        else if (result == ContentDialogResult.None)   // Eliminar todo el grupo
            AppState.Config.ReplaceSessions(_activePhaseName, kept);
        else
            return; // Cancelar
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
