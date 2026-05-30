using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ritmo.Core.Interop;
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
    private IReadOnlyList<CalendarEvent> _calEvents = [];   // eventos del calendario de esta semana (#112)

    // Geometría de la rejilla (debe coincidir con BuildGrid).
    private const double HourColWidth = 64;
    private const double DayColWidth = 150;
    private const double RowHeight = 26;      // alto de media hora
    private const int SlotsPerHour = 2;

    private enum DragMode { None, Move, ResizeH, ResizeV, ResizeBoth }

    // Estado unificado del arrastre (mover / redimensionar) #82/#88/#89/#90.
    private DragMode _mode = DragMode.None;
    private SessionCard? _card;
    private Ritmo.Core.Scheduling.SessionGroup? _group;
    private double _startX, _startY;          // inicio del puntero en coords de GridRoot
    private int _c0, _startSlot, _startDaySpan, _startRowSpan;
    private bool _movedEnough;
    private int _maxDaySpan, _maxRows;         // topes para no solapar (calculados al empezar)
    private int _curCol, _curRow, _curDaySpan, _curRowSpan;   // preview actual (válido)
    private int _startHour = 8;
    private IReadOnlyList<StudySession> _keptForDrag = [];

    public SchedulePage()
    {
        InitializeComponent();
        Loaded += (_, _) => { Build(); _ = LoadCalendarAsync(); };
        // El movimiento/soltar se atienden a nivel de rejilla (con el puntero capturado).
        GridRoot.PointerMoved += GridRoot_PointerMoved;
        GridRoot.PointerReleased += GridRoot_PointerReleased;
        GridRoot.PointerCaptureLost += (_, _) => CancelDrag();
    }

    private void CancelDrag()
    {
        if (_card is not null) _card.Opacity = _group is not null && _group.Representative.IsTentative ? 0.6 : 1.0;
        _mode = DragMode.None; _card = null; _group = null;
    }

    private void GridRoot_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_mode == DragMode.None || _card is null || _group is null) return;
        var pt = e.GetCurrentPoint(GridRoot).Position;

        if (!_movedEnough && (Math.Abs(pt.X - _startX) > 6 || Math.Abs(pt.Y - _startY) > 6))
        {
            _movedEnough = true;
            if (_mode == DragMode.Move) _card.Opacity = 0.6;
        }
        if (!_movedEnough) return;

        if (_mode is DragMode.ResizeH or DragMode.ResizeBoth)
        {
            int targetDay = (int)Math.Floor((pt.X - HourColWidth) / DayColWidth);
            targetDay = Math.Clamp(targetDay, _c0, _c0 + _maxDaySpan - 1);
            _curDaySpan = targetDay - _c0 + 1;
            Grid.SetColumnSpan(_card, _curDaySpan);
        }
        if (_mode is DragMode.ResizeV or DragMode.ResizeBoth)
        {
            int bottomRow = (int)Math.Round((pt.Y - 32) / RowHeight);   // 32 = cabecera
            int span = Math.Clamp(bottomRow - _startSlot + 1, 1, _maxRows);
            _curRowSpan = span;
            Grid.SetRowSpan(_card, _curRowSpan);
        }
        if (_mode == DragMode.Move)
        {
            int dayDelta = (int)Math.Round((pt.X - _startX) / DayColWidth);
            int slotDelta = (int)Math.Round((pt.Y - _startY) / RowHeight);
            int dayIndex = Math.Clamp(_c0 + dayDelta, 0, 7 - _startDaySpan);
            int row = Math.Max(1, _startSlot + slotDelta);
            // Solo aceptar la nueva posición si NO solapa (si solapa, se queda en la última válida).
            if (!MoveCollides(dayIndex, row))
            {
                _curCol = dayIndex + 1; _curRow = row;
                Grid.SetColumn(_card, _curCol); Grid.SetRow(_card, _curRow);
            }
        }
    }

    private bool MoveCollides(int dayIndex, int row)
    {
        var rep = _group!.Representative;
        var start = ScheduleMath.ShiftStart(rep.Start, row - _startSlot);
        var cand = _group.Members.Select(m =>
            rep with { Day = Days[Math.Clamp(Array.IndexOf(Days, m.Day) + (dayIndex - _c0), 0, 6)], Start = start });
        return Collides(cand, _keptForDrag);
    }

    private void GridRoot_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        GridRoot.ReleasePointerCapture(e.Pointer);
        if (_mode == DragMode.None || _card is null || _group is null) { CancelDrag(); return; }

        var mode = _mode; var group = _group; bool moved = _movedEnough;
        if (_card is not null) _card.Opacity = group.Representative.IsTentative ? 0.6 : 1.0;
        _mode = DragMode.None; var card = _card; _card = null; _group = null;

        if (!moved)
        {
            // Un clic sin arrastre (en cualquier zona de la tarjeta) abre el editor (#91).
            _ = ShowEditGroup(group);
            return;
        }

        switch (mode)
        {
            case DragMode.Move:
                _ = ApplyMove(group, (_curCol - 1) - _c0, RowsToSlotDelta(_curRow)); break;
            case DragMode.ResizeH:
                _ = ApplyResize(group, _c0, _curDaySpan); break;
            case DragMode.ResizeV:
                _ = ApplyVerticalResize(group, _curRowSpan); break;
            case DragMode.ResizeBoth:
                _ = ApplyResizeBoth(group, _c0, _curDaySpan, _curRowSpan); break;
        }
    }

    private int RowsToSlotDelta(int newRow) => newRow - _startSlot;

    /// <summary>Empieza un arrastre desde la tarjeta, según la zona pulsada (borde/cuerpo).</summary>
    private void BeginDrag(SessionCard card, Ritmo.Core.Scheduling.SessionGroup group, DragMode mode,
                           int c0, int startSlot, int daySpan, int rowSpan,
                           Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _mode = mode; _card = card; _group = group; _movedEnough = false;
        _c0 = c0; _startSlot = startSlot; _startDaySpan = daySpan; _startRowSpan = rowSpan;
        _curCol = c0 + 1; _curRow = startSlot; _curDaySpan = daySpan; _curRowSpan = rowSpan;
        var pt = e.GetCurrentPoint(GridRoot).Position; _startX = pt.X; _startY = pt.Y;

        // Conservadas (no del grupo) + topes de no-solape, calculados una vez.
        var settings = AppState.Load();
        var phase = settings.Plan.Phases.FirstOrDefault(p => p.Name == _activePhaseName);
        var rep = group.Representative;
        var groupDays = group.Members.Select(m => m.Day).ToHashSet();
        _keptForDrag = phase is null ? []
            : phase.Schedule.Sessions.Where(x => !(SameBlock(x, rep) && groupDays.Contains(x.Day))).ToList();
        _maxDaySpan = MaxDaySpan(rep, c0, rep.Duration, _keptForDrag);
        _maxRows = MaxRows(rep, groupDays, _keptForDrag);

        GridRoot.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    /// <summary>Máximo nº de columnas contiguas (desde c0) sin pisar otra sesión.</summary>
    private int MaxDaySpan(StudySession rep, int c0, TimeSpan dur, IReadOnlyList<StudySession> kept)
    {
        int max = 1;
        for (int d = c0 + 1; d <= 6; d++)
        {
            bool collide = kept.Any(k => k.Day == Days[d] && ScheduleMath.TimesOverlap(rep.Start, dur, k.Start, k.Duration));
            if (collide) break;
            max = d - c0 + 1;
        }
        return max;
    }

    /// <summary>Máximo nº de filas (medias horas) que puede durar sin pisar nada debajo en ningún día.</summary>
    private int MaxRows(StudySession rep, System.Collections.Generic.HashSet<DayOfWeek> groupDays, IReadOnlyList<StudySession> kept)
    {
        int startMin = rep.Start.Hour * 60 + rep.Start.Minute;
        int maxEnd = 24 * 60;
        foreach (var day in groupDays)
        {
            foreach (var k in kept.Where(k => k.Day == day))
            {
                int kStart = k.Start.Hour * 60 + k.Start.Minute;
                if (kStart >= startMin && kStart < maxEnd) maxEnd = kStart;   // sesión que empieza por debajo
            }
        }
        int slotMin = 60 / SlotsPerHour;
        return Math.Max(1, (maxEnd - startMin) / slotMin);
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

        _startHour = startH;
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

            // Border visual de la tarjeta (dentro de un SessionCard para poder cambiar el cursor).
            var visual = new Border
            {
                Background = baseColor,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 3, 6, 3),
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
            var card = new SessionCard
            {
                Content = visual,
                Margin = new Thickness(2),
                Opacity = s.IsTentative ? 0.6 : 1.0,
                Tag = group
            };

            // Hover: borde de acento + cursor según la zona (cuerpo=mover, bordes/esquina=redimensionar).
            double restThickness = isActive ? 2 : 0;
            int cardStartSlot = startSlot, cardSpanSlots = spanSlots, cardDayCol = dayCol;
            var thisGroup = group;
            card.PointerEntered += (_, _) => {
                visual.BorderThickness = new Thickness(1.5);
                visual.BorderBrush = ScheduleColors.TextFor(s.Kind);
            };
            card.PointerExited += (_, _) => {
                visual.BorderThickness = new Thickness(restThickness);
                visual.BorderBrush = isActive ? new SolidColorBrush(accentColor) : null;
            };
            card.PointerMoved += (o, e) =>
            {
                if (_mode != DragMode.None) return;   // durante un arrastre no cambies el cursor
                var b = (SessionCard)o;
                b.SetCursor(ZoneCursor(b, e));
            };
            card.PointerPressed += (o, e) =>
            {
                var b = (SessionCard)o;
                BeginDrag(b, thisGroup, ZoneMode(b, e), cardDayCol, cardStartSlot, thisGroup.DaySpan, cardSpanSlots, e);
            };
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

        OverlayCalendar(g, startH, hours);   // eventos del calendario sobre la rejilla (#112)
    }

    // ---------- Calendarios externos sobre la rejilla (#112) ----------

    private static readonly Windows.UI.Color[] CalPalette =
    {
        Windows.UI.Color.FromArgb(255, 38, 166, 154),   // teal
        Windows.UI.Color.FromArgb(255, 126, 87, 194),   // morado
        Windows.UI.Color.FromArgb(255, 236, 64, 122),   // rosa
        Windows.UI.Color.FromArgb(255, 255, 160, 0),    // ámbar
        Windows.UI.Color.FromArgb(255, 41, 121, 255),   // azul
    };

    private async Task LoadCalendarAsync()
    {
        var settings = AppState.Load();
        if (settings.CalendarFeeds.Count == 0) return;
        var today = DateOnly.FromDateTime(DateTime.Now);
        int offset = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var weekStart = today.AddDays(-offset);
        try { _calEvents = await CalendarService.FetchAsync(settings.CalendarFeeds, weekStart, weekStart.AddDays(6)); }
        catch { return; }
        if (_calEvents.Count > 0) Build();   // re-pinta con el overlay (sin volver a descargar)
    }

    private void OverlayCalendar(Grid g, int startH, int hours)
    {
        if (_calEvents.Count == 0) return;
        int maxRow = 1 + hours * SlotsPerHour;
        var colorByCal = new Dictionary<string, Windows.UI.Color>(StringComparer.OrdinalIgnoreCase);
        int nextColor = 0;

        foreach (var ev in _calEvents)
        {
            int dayCol = Array.IndexOf(Days, ev.Start.DayOfWeek);
            if (dayCol < 0) continue;

            var cal = string.IsNullOrEmpty(ev.Calendar) ? "Calendario" : ev.Calendar!;
            if (!colorByCal.TryGetValue(cal, out var color)) { color = CalPalette[nextColor++ % CalPalette.Length]; colorByCal[cal] = color; }

            int startSlot, spanSlots;
            if (ev.AllDay) { startSlot = 1; spanSlots = 1; }
            else
            {
                double startHours = ev.Start.Hour + ev.Start.Minute / 60.0 - startH;
                if (startHours >= hours) continue;
                startSlot = 1 + (int)Math.Round(Math.Max(0, startHours) * SlotsPerHour);
                spanSlots = Math.Max(1, (int)Math.Round((ev.End - ev.Start).TotalHours * SlotsPerHour));
                if (startSlot + spanSlots > maxRow) spanSlots = Math.Max(1, maxRow - startSlot);
            }

            var fill = color; fill.A = 210;             // translúcido (deja ver el bloque debajo)
            var meta = ev.AllDay ? "Todo el día" : $"{ev.Start:HH\\:mm}–{ev.End:HH\\:mm}";
            var card = new Border
            {
                Background = new SolidColorBrush(fill),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(4, 0, 0, 0),     // barra de acento a la izquierda
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(3, 1, 3, 1),
                Padding = new Thickness(5, 2, 4, 2),
                IsHitTestVisible = false,                         // read-only: no estorba el arrastre
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = ev.Title, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = $"{meta} · {cal}", FontSize = 9, Opacity = 0.9,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), TextTrimming = TextTrimming.CharacterEllipsis }
                    }
                }
            };
            Grid.SetRow(card, startSlot); Grid.SetRowSpan(card, spanSlots);
            Grid.SetColumn(card, dayCol + 1);
            g.Children.Add(card);
        }
    }

    /// <summary>Lleva al temporizador y arranca la concentración del bloque activo.</summary>
    private void FocusNow() => Navigator.GoToTimer(this, autoStart: true);

    // ---------- Zonas del cursor (borde derecho = días, inferior = duración, esquina = ambas) ----------

    private const double EdgePx = 12;

    private static (bool right, bool bottom) Zone(SessionCard card, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(card).Position;
        return (p.X >= card.ActualWidth - EdgePx, p.Y >= card.ActualHeight - EdgePx);
    }

    private static InputSystemCursorShape ZoneCursor(SessionCard card, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var (r, b) = Zone(card, e);
        return (r && b) ? InputSystemCursorShape.SizeNorthwestSoutheast
             : r ? InputSystemCursorShape.SizeWestEast
             : b ? InputSystemCursorShape.SizeNorthSouth
             : InputSystemCursorShape.SizeAll;
    }

    private static DragMode ZoneMode(SessionCard card, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var (r, b) = Zone(card, e);
        return (r && b) ? DragMode.ResizeBoth : r ? DragMode.ResizeH : b ? DragMode.ResizeV : DragMode.Move;
    }

    /// <summary>¿Algún candidato pisa (mismo día, horario solapado) una sesión conservada? #88</summary>
    private static bool Collides(IEnumerable<StudySession> candidates, IReadOnlyList<StudySession> kept)
    {
        foreach (var c in candidates)
            foreach (var k in kept)
                if (c.Day == k.Day && ScheduleMath.TimesOverlap(c.Start, c.Duration, k.Start, k.Duration))
                    return true;
        return false;
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
                                .Select(i => rep with { Day = Days[i] }).ToList();

        if (Collides(rebuilt, kept)) { Build(); return; }   // no pisar otra sesión (#88)

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
            .Select(i => rep with { Day = Days[i], Start = newStart })
            .ToList();

        if (Collides(moved, kept)) { Build(); return; }   // no pisar otra sesión (#88)

        AppState.Config.ReplaceSessions(_activePhaseName, [.. kept, .. moved]);
        await Task.CompletedTask;
        Build();
    }

    /// <summary>
    /// Redimensión vertical: cambia la DURACIÓN del grupo a <paramref name="newSpanRows"/>
    /// filas (cada fila = 30 min). Si chocaría con una sesión de debajo, se revierte (#90).
    /// </summary>
    private async Task ApplyVerticalResize(Ritmo.Core.Scheduling.SessionGroup group, int newSpanRows)
    {
        if (_activePhaseName is null) { Build(); return; }
        var settings = AppState.Load();
        var phase = settings.Plan.Phases.FirstOrDefault(p => p.Name == _activePhaseName);
        if (phase is null) { Build(); return; }

        var rep = group.Representative;
        var origDays = group.Members.Select(m => m.Day).ToHashSet();
        bool Belongs(StudySession x) => SameBlock(x, rep) && origDays.Contains(x.Day);
        var kept = phase.Schedule.Sessions.Where(x => !Belongs(x)).ToList();

        var newDuration = TimeSpan.FromMinutes(Math.Max(1, newSpanRows) * (60 / SlotsPerHour));
        var resized = group.Members.Select(m => rep with { Day = m.Day, Duration = newDuration }).ToList();

        if (Collides(resized, kept)) { Build(); return; }   // chocaría con algo debajo (#90)

        AppState.Config.ReplaceSessions(_activePhaseName, [.. kept, .. resized]);
        await Task.CompletedTask;
        Build();
    }

    /// <summary>Redimensión en esquina: cambia días Y duración a la vez.</summary>
    private async Task ApplyResizeBoth(Ritmo.Core.Scheduling.SessionGroup group, int c0, int daySpan, int rowSpan)
    {
        if (_activePhaseName is null) { Build(); return; }
        var settings = AppState.Load();
        var phase = settings.Plan.Phases.FirstOrDefault(p => p.Name == _activePhaseName);
        if (phase is null) { Build(); return; }

        var rep = group.Representative;
        var origDays = group.Members.Select(m => m.Day).ToHashSet();
        bool Belongs(StudySession x) => SameBlock(x, rep) && origDays.Contains(x.Day);
        var kept = phase.Schedule.Sessions.Where(x => !Belongs(x)).ToList();

        var newDuration = TimeSpan.FromMinutes(Math.Max(1, rowSpan) * (60 / SlotsPerHour));
        var rebuilt = Enumerable.Range(c0, Math.Clamp(daySpan, 1, 7 - c0))
                                .Select(i => rep with { Day = Days[i], Duration = newDuration }).ToList();

        if (Collides(rebuilt, kept)) { Build(); return; }

        AppState.Config.ReplaceSessions(_activePhaseName, [.. kept, .. rebuilt]);
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
