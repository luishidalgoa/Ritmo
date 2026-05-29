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

    // Geometría de la rejilla (debe coincidir con BuildGrid).
    private const double HourColWidth = 64;
    private const double DayColWidth = 150;
    private const double RowHeight = 26;      // alto de media hora
    private const int SlotsPerHour = 2;

    // Estado del arrastre de redimensión horizontal (#82).
    private bool _dragging;
    private Border? _dragCard;
    private Ritmo.Core.Scheduling.SessionGroup? _dragGroup;
    private int _dragC0;

    // Estado del arrastre para MOVER una sesión (#82).
    private bool _movePressed, _moving;
    private double _moveStartX, _moveStartY;
    private Border? _moveCard;
    private Ritmo.Core.Scheduling.SessionGroup? _moveGroup;
    private int _moveStartSlot;

    public SchedulePage()
    {
        InitializeComponent();
        Loaded += (_, _) => Build();
        // Durante el arrastre, los movimientos/soltar se atienden a nivel de la
        // rejilla (con el puntero capturado), no del asa minúscula (#82).
        GridRoot.PointerMoved += GridRoot_PointerMoved;
        GridRoot.PointerReleased += GridRoot_PointerReleased;
        GridRoot.PointerCaptureLost += (_, _) =>
        {
            _dragging = false;
            if (_movePressed) { _movePressed = false; _moving = false; if (_moveCard is not null) _moveCard.Opacity = 1.0; _moveCard = null; _moveGroup = null; }
        };
    }

    private void GridRoot_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(GridRoot).Position;

        // Redimensión por el asa derecha.
        if (_dragging && _dragCard is not null)
        {
            int targetDay = (int)Math.Floor((pt.X - HourColWidth) / DayColWidth);
            targetDay = Math.Clamp(targetDay, _dragC0, 6);
            Grid.SetColumnSpan(_dragCard, targetDay - _dragC0 + 1);
            return;
        }

        // Mover (arrastrar el cuerpo). Empieza al superar un umbral, para no robar el clic.
        if (_movePressed && _moveCard is not null && _moveGroup is not null)
        {
            if (!_moving && (Math.Abs(pt.X - _moveStartX) > 6 || Math.Abs(pt.Y - _moveStartY) > 6))
            {
                _moving = true;
                _moveCard.Opacity = 0.6;   // feedback de "agarrado"
            }
            if (_moving)
            {
                int dayDelta = (int)Math.Round((pt.X - _moveStartX) / DayColWidth);
                int slotDelta = (int)Math.Round((pt.Y - _moveStartY) / RowHeight);
                int newCol = Math.Clamp(_moveGroup.FirstDayIndex + dayDelta, 0, 7 - _moveGroup.DaySpan) + 1;
                int newRow = Math.Max(1, _moveStartSlot + slotDelta);
                Grid.SetColumn(_moveCard, newCol);
                Grid.SetRow(_moveCard, newRow);
            }
        }
    }

    private void GridRoot_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        GridRoot.ReleasePointerCapture(e.Pointer);

        if (_dragging)
        {
            _dragging = false;
            var card = _dragCard; var group = _dragGroup; var c0 = _dragC0;
            _dragCard = null; _dragGroup = null;
            if (card is not null && group is not null)
                _ = ApplyResize(group, c0, Grid.GetColumnSpan(card));
            return;
        }

        if (_movePressed)
        {
            var pt = e.GetCurrentPoint(GridRoot).Position;
            bool wasMoving = _moving;
            var group = _moveGroup;
            _movePressed = false; _moving = false;
            var card = _moveCard; _moveCard = null; _moveGroup = null;
            if (card is not null) card.Opacity = 1.0;

            if (group is null) return;
            if (wasMoving)
            {
                int dayDelta = (int)Math.Round((pt.X - _moveStartX) / DayColWidth);
                int slotDelta = (int)Math.Round((pt.Y - _moveStartY) / RowHeight);
                _ = ApplyMove(group, dayDelta, slotDelta);
            }
            else
            {
                _ = ShowEditGroup(group);   // fue un clic, no un arrastre -> editar
            }
        }
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

        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HourColWidth) });
        for (int c = 0; c < 7; c++)
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DayColWidth) });

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
            // Arrastrar el cuerpo = mover; un clic sin arrastre = editar (#82).
            int cardStartSlot = startSlot;
            card.PointerPressed += (o, e) =>
            {
                if (_dragging) return;   // hay una redimensión en curso
                var pt = e.GetCurrentPoint(GridRoot).Position;
                _movePressed = true; _moving = false;
                _moveStartX = pt.X; _moveStartY = pt.Y;
                _moveCard = (Border)o; _moveGroup = (Ritmo.Core.Scheduling.SessionGroup)((Border)o).Tag;
                _moveStartSlot = cardStartSlot;
                GridRoot.CapturePointer(e.Pointer);
                e.Handled = true;
            };
            Grid.SetRow(card, startSlot); Grid.SetRowSpan(card, spanSlots);
            Grid.SetColumn(card, dayCol + 1); Grid.SetColumnSpan(card, group.DaySpan);
            g.Children.Add(card);

            // Asa de redimensión en el borde derecho: arrastra para expandir/encoger por días (#82).
            AddResizeHandle(g, card, group, startSlot, spanSlots, accentColor);

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

    // ---------- Redimensión horizontal por arrastre (#82) ----------

    private void AddResizeHandle(Grid g, Border card, Ritmo.Core.Scheduling.SessionGroup group,
                                 int startSlot, int spanSlots, Windows.UI.Color accentColor)
    {
        int c0 = group.FirstDayIndex;
        var handle = new Border
        {
            Width = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 2, 2, 2),
            CornerRadius = new CornerRadius(0, 6, 6, 0),
            Background = new SolidColorBrush(accentColor),
            Opacity = 0                           // visible solo al pasar el ratón
        };
        ToolTipService.SetToolTip(handle, "Arrastra para extender por días");

        handle.PointerEntered += (o, _) => { if (!_dragging) ((Border)o).Opacity = 0.85; };
        handle.PointerExited += (o, _) => { if (!_dragging) ((Border)o).Opacity = 0.35; };
        // Aparece como grip al pasar el ratón por la tarjeta (afordancia de "arrastra para extender").
        card.PointerEntered += (_, _) => { if (!_dragging) handle.Opacity = 0.55; };
        card.PointerExited += (_, _) => { if (!_dragging) handle.Opacity = 0; };

        handle.PointerPressed += (o, e) =>
        {
            _dragging = true; _dragCard = card; _dragGroup = group; _dragC0 = c0;
            ((Border)o).Opacity = 0.9;
            GridRoot.CapturePointer(e.Pointer);   // captura a nivel de rejilla (mueve fuera del asa)
            e.Handled = true;                      // evita que el Tapped de la tarjeta abra el editor
        };

        Grid.SetRow(handle, startSlot); Grid.SetRowSpan(handle, spanSlots);
        Grid.SetColumn(handle, c0 + group.DaySpan);   // columna de rejilla del borde derecho de la tarjeta
        g.Children.Add(handle);
    }

    /// <summary>
    /// Aplica la nueva extensión: el bloque pasa a existir en los días
    /// [c0 .. c0+newSpan-1] (añade los que falten, quita los sobrantes).
    /// </summary>
    private async Task ApplyResize(Ritmo.Core.Scheduling.SessionGroup group, int c0, int newSpan)
    {
        if (_activePhaseName is null) { Build(); return; }
        var settings = AppState.Load();
        var phase = settings.Plan.Phases.FirstOrDefault(p => p.Name == _activePhaseName);
        if (phase is null) { Build(); return; }

        var rep = group.Representative;
        var origDays = group.Members.Select(m => m.Day).ToHashSet();
        bool Belongs(StudySession x) => SameBlock(x, rep) && origDays.Contains(x.Day);

        var kept = phase.Schedule.Sessions.Where(x => !Belongs(x)).ToList();
        var rebuilt = Enumerable.Range(c0, Math.Clamp(newSpan, 1, 7 - c0))
                                .Select(i => rep with { Day = Days[i] });

        AppState.Config.ReplaceSessions(_activePhaseName, [.. kept, .. rebuilt]);
        await Task.CompletedTask;
        Build();
    }

    /// <summary>
    /// Mueve el grupo: desplaza sus días en <paramref name="dayDelta"/> columnas y su
    /// hora de inicio en <paramref name="slotDelta"/> medias horas (manteniendo duración).
    /// </summary>
    private async Task ApplyMove(Ritmo.Core.Scheduling.SessionGroup group, int dayDelta, int slotDelta)
    {
        if (_activePhaseName is null) { Build(); return; }
        if (dayDelta == 0 && slotDelta == 0) { Build(); return; }   // no se movió de verdad

        var settings = AppState.Load();
        var phase = settings.Plan.Phases.FirstOrDefault(p => p.Name == _activePhaseName);
        if (phase is null) { Build(); return; }

        var rep = group.Representative;
        var origDays = group.Members.Select(m => m.Day).ToHashSet();
        bool Belongs(StudySession x) => SameBlock(x, rep) && origDays.Contains(x.Day);
        var kept = phase.Schedule.Sessions.Where(x => !Belongs(x)).ToList();

        // Nueva hora de inicio (mantiene duración), acotada al día.
        var newStart = ScheduleMath.ShiftStart(rep.Start, slotDelta);

        // Cada miembro cambia de día por el delta (acotado a la semana); dedup por día.
        var moved = group.Members
            .Select(m => Math.Clamp(Array.IndexOf(Days, m.Day) + dayDelta, 0, 6))
            .Distinct()
            .Select(i => rep with { Day = Days[i], Start = newStart });

        AppState.Config.ReplaceSessions(_activePhaseName, [.. kept, .. moved]);
        await Task.CompletedTask;
        Build();
    }

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
