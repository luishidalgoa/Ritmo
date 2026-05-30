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
using Ritmo.Core.Scheduling;
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
    private IReadOnlyList<CalendarEvent> _calEvents = [];   // eventos del calendario de la semana mostrada (#112)
    private DateOnly _weekStart = MondayOf(DateOnly.FromDateTime(DateTime.Now));   // lunes de la semana mostrada (#113)

    private static DateOnly MondayOf(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));

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

    // Panel de detalle y resolución de solapamientos (#114).
    private IReadOnlyList<StudySession> _sessions = [];        // sesiones de la fase visible (para detectar conflictos)
    private IReadOnlyList<OverlapPriority> _priorities = [];   // decisiones de prioridad guardadas
    private string? _selectedSessionKey;                       // sesión resaltada en la rejilla
    private string? _selectedEventKey;                         // evento resaltado en la rejilla
    private SessionGroup? _selectedGroup;                      // grupo mostrado en el panel
    private CalendarEvent? _selectedEvent;                     // evento mostrado en el panel

    public SchedulePage()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshWeek();
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

        // El clic (sin arrastre) lo gestiona el gesto Tapped de la tarjeta (#114),
        // que es fiable aunque el ScrollViewer robe la captura del puntero.
        if (!moved) return;

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

        // Nota: NO marcamos e.Handled — eso suprimiría el gesto Tapped de la tarjeta
        // (que es como se abre el detalle). El arrastre usa la captura del puntero.
        GridRoot.CapturePointer(e.Pointer);
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
        _sessions = schedule.Sessions;
        _priorities = settings.OverlapPriorities;
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

        // "En vivo" (#69): hoy + ahora + bloque activo — SOLO si miramos la semana actual (#113).
        var now = DateTime.Now;
        bool isCurrentWeek = _weekStart == MondayOf(DateOnly.FromDateTime(now));
        int todayCol = isCurrentWeek ? Array.IndexOf(Days, now.DayOfWeek) : -1;
        var activeSession = isCurrentWeek ? new Ritmo.Core.Scheduling.SchedulePlanner(schedule).GetActiveSession(now) : null;
        var accentColor = ((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color;
        var todayHeaderBrush = new SolidColorBrush(accentColor) { Opacity = 0.22 };
        var todayTint = new SolidColorBrush(accentColor) { Opacity = 0.06 };

        for (int c = 0; c < 7; c++)
        {
            bool isToday = c == todayCol;
            g.Children.Add(Cell(new TextBlock {
                Text = $"{DayNames[c]} {_weekStart.AddDays(c).Day}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
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
            bool isSelected = _selectedSessionKey is not null && SessionKey(s) == _selectedSessionKey;
            bool ring = isActive || isSelected;   // borde de acento: bloque activo o seleccionado en el panel

            // Border visual de la tarjeta (dentro de un SessionCard para poder cambiar el cursor).
            var visual = new Border
            {
                Background = baseColor,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 3, 6, 3),
                BorderBrush = ring ? new SolidColorBrush(accentColor) : null,
                BorderThickness = ring ? new Thickness(2) : new Thickness(0),
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
            double restThickness = ring ? 2 : 0;
            int cardStartSlot = startSlot, cardSpanSlots = spanSlots, cardDayCol = dayCol;
            var thisGroup = group;
            card.PointerEntered += (_, _) => {
                visual.BorderThickness = new Thickness(1.5);
                visual.BorderBrush = ScheduleColors.TextFor(s.Kind);
            };
            card.PointerExited += (_, _) => {
                visual.BorderThickness = new Thickness(restThickness);
                visual.BorderBrush = ring ? new SolidColorBrush(accentColor) : null;
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
            // Clic = abrir el detalle. Tapped es fiable dentro del ScrollViewer (no
            // depende de la captura del puntero, que el SV roba); el mismo mecanismo
            // que usan los eventos del calendario. El arrastre sigue por Pointer*. #114
            card.Tapped += (_, _) => ShowSessionDetail(thisGroup);
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

    // ---------- Navegación entre semanas (#113) ----------

    private void PrevWeekBtn_Click(object sender, RoutedEventArgs e) { _weekStart = _weekStart.AddDays(-7); RefreshWeek(); }
    private void NextWeekBtn_Click(object sender, RoutedEventArgs e) { _weekStart = _weekStart.AddDays(7); RefreshWeek(); }
    private void ThisWeekBtn_Click(object sender, RoutedEventArgs e) { _weekStart = MondayOf(DateOnly.FromDateTime(DateTime.Now)); RefreshWeek(); }

    /// <summary>Cambia a la semana mostrada: actualiza la etiqueta, repinta y re-descarga sus eventos.</summary>
    private void RefreshWeek()
    {
        var es = new System.Globalization.CultureInfo("es-ES");
        WeekLabel.Text = $"{_weekStart.ToString("d MMM", es)} – {_weekStart.AddDays(6).ToString("d MMM yyyy", es)}";
        _calEvents = [];        // limpia el overlay de la semana anterior mientras descarga
        CloseDetail(internalRefresh: true);   // la selección de otra semana ya no aplica
        Build();
        _ = LoadCalendarAsync();
    }

    private async Task LoadCalendarAsync()
    {
        var settings = AppState.Load();
        if (settings.CalendarFeeds.Count == 0) return;
        var target = _weekStart;
        IReadOnlyList<CalendarEvent> events;
        try { events = await CalendarService.FetchAsync(settings.CalendarFeeds, target, target.AddDays(6)); }
        catch { return; }
        if (target != _weekStart) return;   // el usuario ya cambió de semana: descarta
        _calEvents = events;
        Build();   // re-pinta con el overlay (sin volver a descargar)
    }

    /// <summary>Índice de color estable y determinista para un nombre de calendario.</summary>
    private static int CalColorIndex(string cal)
    {
        int h = 0;
        foreach (var ch in cal) h = (h * 31 + ch) & 0x7fffffff;
        return h % CalPalette.Length;
    }

    private static readonly Windows.UI.Color WarnColor = Windows.UI.Color.FromArgb(255, 245, 166, 35);   // ámbar "sin decidir"

    private void OverlayCalendar(Grid g, int startH, int hours)
    {
        if (_calEvents.Count == 0) return;
        int maxRow = 1 + hours * SlotsPerHour;
        var accent = ((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color;

        foreach (var ev in _calEvents)
        {
            int dayCol = Array.IndexOf(Days, ev.Start.DayOfWeek);
            if (dayCol < 0) continue;

            var cal = string.IsNullOrEmpty(ev.Calendar) ? "Calendario" : ev.Calendar!;
            var color = CalPalette[CalColorIndex(cal)];   // color estable por nombre de calendario

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

            // ¿Pisa alguna sesión? ¿Qué prioridad eligió el usuario? (#114)
            bool conflict = OverlapResolver.SessionsOverlapping(ev, _sessions).Count > 0;
            bool? prefer = _priorities.FirstOrDefault(p => p.EventKey == OverlapResolver.EventKey(ev))?.PreferCalendar;
            bool isSelected = _selectedEventKey is not null && OverlapResolver.EventKey(ev) == _selectedEventKey;

            // Quién gana: el alfa del fondo da el matiz, pero la atenuación cuando
            // gana la SESIÓN se aplica a TODA la tarjeta (fondo + barra + texto) con
            // la opacidad del Border, para que el evento recede de verdad y no quede
            // su barra/título flotando por encima. (#114)
            byte alpha;
            double cardOpacity = 1.0;
            if (!conflict) alpha = 210;
            else if (prefer == true) alpha = 245;                       // evento gana: opaco
            else if (prefer == false) { alpha = 200; cardOpacity = 0.28; }   // sesión gana: el evento recede entero
            else alpha = 150;                                            // sin decidir: medio (se ven ambos)
            var fill = color; fill.A = alpha;
            string warn = conflict && prefer is null ? "⚠ " : "";       // ⚠ sin decidir

            var white = new SolidColorBrush(Microsoft.UI.Colors.White);
            var content = new StackPanel { Spacing = 0 };
            if (spanSlots <= 1)
            {
                // Muy corto / todo el día: una sola línea que quepa en ~26px.
                var oneLine = ev.AllDay ? ev.Title : $"{ev.Start:HH\\:mm}  {ev.Title}";
                content.Children.Add(new TextBlock { Text = warn + oneLine, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = white, TextTrimming = TextTrimming.CharacterEllipsis });
            }
            else
            {
                var meta = ev.AllDay ? "Todo el día" : $"{ev.Start:HH\\:mm}–{ev.End:HH\\:mm}";
                content.Children.Add(new TextBlock { Text = warn + ev.Title, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = white, TextTrimming = TextTrimming.CharacterEllipsis });
                content.Children.Add(new TextBlock { Text = $"{meta} · {cal}", FontSize = 9, Opacity = 0.9,
                    Foreground = white, TextTrimming = TextTrimming.CharacterEllipsis });
            }

            var borderColor = isSelected ? accent : (conflict && prefer is null ? WarnColor : color);
            var borderThk = isSelected || (conflict && prefer is null) ? new Thickness(2) : new Thickness(4, 0, 0, 0);

            var card = new Border
            {
                Background = new SolidColorBrush(fill),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = borderThk,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(3, 1, 3, 1),                // ancho completo del día
                Padding = new Thickness(5, 1, 4, 1),
                VerticalAlignment = VerticalAlignment.Stretch,
                Opacity = cardOpacity,                            // gana la sesión -> toda la tarjeta recede (#114)
                Child = content                                   // clicable: abre el detalle del evento (#114)
            };
            var captured = ev;
            card.Tapped += (_, _) => ShowEventDetail(captured);
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

    // ---------- Panel lateral de detalle + resolución de solapamientos (#114) ----------

    /// <summary>Identidad estable de una sesión, para resaltarla tras repintar la rejilla.</summary>
    private static string SessionKey(StudySession s)
        => $"{s.Title.Trim()}|{s.Kind}|{s.Start}|{s.Duration}|{s.IsTentative}";

    /// <summary>Muestra el detalle completo de una sesión (y sus solapamientos) en el panel.</summary>
    private void ShowSessionDetail(SessionGroup group)
    {
        var rep = group.Representative;
        _selectedGroup = group; _selectedEvent = null;
        _selectedSessionKey = SessionKey(rep); _selectedEventKey = null;
        Build();   // repinta la rejilla con el resaltado

        var content = DetailContent;
        content.Children.Clear();
        content.Children.Add(DetailHeader("Detalle de la sesión"));
        content.Children.Add(TitleRow(ScheduleColors.For(rep.Kind), rep.Title));

        var meta = new StackPanel { Spacing = 4 };
        meta.Children.Add(MetaLine(rep.Kind.Label() + (rep.IsTentative ? "  ·  provisional" : "")));
        meta.Children.Add(MetaLine($"{rep.Start:HH\\:mm} – {rep.End:HH\\:mm}  ·  {FormatDuration(rep.Duration)}"));
        var days = group.Members.Select(m => m.Day).Distinct()
                        .OrderBy(d => Array.IndexOf(Days, d)).Select(ShortDay);
        meta.Children.Add(MetaLine("Se repite: " + string.Join(" · ", days)));
        if (_activePhaseName is not null) meta.Children.Add(MetaLine("Fase: " + _activePhaseName));
        content.Children.Add(meta);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var editBtn = new Button { Content = "Editar" };
        editBtn.Click += async (_, _) => { await ShowEditGroup(group); CloseDetail(); };
        var focusBtn = new Button { Content = "Concentrarme", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        focusBtn.Click += (_, _) => FocusNow();
        actions.Children.Add(editBtn); actions.Children.Add(focusBtn);
        content.Children.Add(actions);

        // Solapamientos con el calendario en los días en que se repite la sesión.
        var resolvers = new List<FrameworkElement>();
        foreach (var day in group.Members.Select(m => m.Day).Distinct().OrderBy(d => Array.IndexOf(Days, d)))
        {
            int di = Array.IndexOf(Days, day);
            if (di < 0) continue;
            var date = _weekStart.AddDays(di);
            var occurrence = rep with { Day = day };
            foreach (var ev in OverlapResolver.EventsOverlapping(occurrence, date, _calEvents))
                resolvers.Add(BuildOverlapResolver(ev, [occurrence], date));
        }
        if (resolvers.Count > 0)
        {
            content.Children.Add(SectionLabel("SOLAPAMIENTOS"));
            foreach (var r in resolvers) content.Children.Add(r);
        }

        DetailPanel.Visibility = Visibility.Visible;
    }

    /// <summary>Muestra el detalle de un evento del calendario (y su resolución si pisa una sesión).</summary>
    private void ShowEventDetail(CalendarEvent ev)
    {
        _selectedEvent = ev; _selectedGroup = null;
        _selectedEventKey = OverlapResolver.EventKey(ev); _selectedSessionKey = null;
        Build();

        var es = new System.Globalization.CultureInfo("es-ES");
        var cal = string.IsNullOrEmpty(ev.Calendar) ? "Calendario" : ev.Calendar!;
        var color = new SolidColorBrush(CalPalette[CalColorIndex(cal)]);

        var content = DetailContent;
        content.Children.Clear();
        content.Children.Add(DetailHeader("Detalle del evento"));
        content.Children.Add(TitleRow(color, ev.Title));

        var meta = new StackPanel { Spacing = 4 };
        meta.Children.Add(MetaLine(Capitalize(ev.Start.ToString("dddd d 'de' MMMM", es))));
        meta.Children.Add(MetaLine(ev.AllDay ? "Todo el día" : $"{ev.Start:HH\\:mm} – {ev.End:HH\\:mm}"));
        meta.Children.Add(MetaLine("Calendario: " + cal));
        content.Children.Add(meta);

        var overlapping = OverlapResolver.SessionsOverlapping(ev, _sessions);
        if (overlapping.Count > 0)
        {
            content.Children.Add(SectionLabel("SOLAPAMIENTOS"));
            content.Children.Add(BuildOverlapResolver(ev, overlapping, DateOnly.FromDateTime(ev.Start)));
        }
        else
        {
            content.Children.Add(new TextBlock
            {
                Text = "Evento de solo lectura del calendario suscrito.",
                Opacity = 0.6, FontSize = 12, TextWrapping = TextWrapping.Wrap
            });
        }

        DetailPanel.Visibility = Visibility.Visible;
    }

    /// <summary>Bloque "ver ambas y priorizar" para un evento que pisa una o más sesiones.</summary>
    private FrameworkElement BuildOverlapResolver(CalendarEvent ev, IReadOnlyList<StudySession> sessions, DateOnly date)
    {
        var key = OverlapResolver.EventKey(ev);
        bool? prefer = _priorities.FirstOrDefault(p => p.EventKey == key)?.PreferCalendar;

        var box = new StackPanel { Spacing = 8 };

        var sess = sessions.FirstOrDefault();
        if (sess is not null)
            box.Children.Add(ConflictRow(ScheduleColors.For(sess.Kind), $"Sesión · {sess.Title}",
                $"{sess.Start:HH\\:mm}–{sess.End:HH\\:mm}", winner: prefer == false));

        var cal = string.IsNullOrEmpty(ev.Calendar) ? "Calendario" : ev.Calendar!;
        box.Children.Add(ConflictRow(new SolidColorBrush(CalPalette[CalColorIndex(cal)]), $"Calendario · {ev.Title}",
            ev.AllDay ? "Todo el día" : $"{ev.Start:HH\\:mm}–{ev.End:HH\\:mm}", winner: prefer == true));

        box.Children.Add(new TextBlock { Text = "¿Cuál priorizas?", FontSize = 12, Opacity = 0.7, Margin = new Thickness(0, 2, 0, 0) });

        var choice = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var sBtn = ChoiceButton("La sesión", prefer == false);
        sBtn.Click += (_, _) => { AppState.Config.SetOverlapPriority(key, preferCalendar: false); ReopenAfterPriority(); };
        var cBtn = ChoiceButton("El evento", prefer == true);
        cBtn.Click += (_, _) => { AppState.Config.SetOverlapPriority(key, preferCalendar: true); ReopenAfterPriority(); };
        choice.Children.Add(sBtn); choice.Children.Add(cBtn);
        box.Children.Add(choice);

        if (prefer is not null)
        {
            var clear = new HyperlinkButton { Content = "Quitar prioridad", FontSize = 12, Padding = new Thickness(0) };
            clear.Click += (_, _) => { AppState.Config.ClearOverlapPriority(key); ReopenAfterPriority(); };
            box.Children.Add(clear);
        }

        return new Border
        {
            Padding = new Thickness(12), CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = box
        };
    }

    /// <summary>Tras elegir prioridad: repinta la rejilla y refresca el panel en su contexto.</summary>
    private void ReopenAfterPriority()
    {
        if (_selectedEvent is not null) ShowEventDetail(_selectedEvent);
        else if (_selectedGroup is not null) ShowSessionDetail(_selectedGroup);
    }

    /// <summary>Cierra el panel y quita el resaltado.</summary>
    private void CloseDetail(bool internalRefresh = false)
    {
        DetailPanel.Visibility = Visibility.Collapsed;
        DetailContent.Children.Clear();
        _selectedSessionKey = null; _selectedEventKey = null;
        _selectedGroup = null; _selectedEvent = null;
        if (!internalRefresh) Build();   // repinta sin resaltado (RefreshWeek ya repinta por su cuenta)
    }

    // ---------- Bloques de UI del panel ----------

    private FrameworkElement DetailHeader(string title)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var t = new TextBlock { Text = title, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(t, 0);
        var close = new Button
        {
            Content = new SymbolIcon(Symbol.Cancel), MinWidth = 0, Padding = new Thickness(6),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0)
        };
        ToolTipService.SetToolTip(close, "Cerrar");
        close.Click += (_, _) => CloseDetail();
        Grid.SetColumn(close, 1);
        grid.Children.Add(t); grid.Children.Add(close);
        return grid;
    }

    private static FrameworkElement TitleRow(Brush color, string title)
    {
        var g = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var dot = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = color, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 5, 10, 0) };
        Grid.SetColumn(dot, 0);
        var t = new TextBlock { Text = title, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(t, 1);
        g.Children.Add(dot); g.Children.Add(t);
        return g;
    }

    private static TextBlock MetaLine(string text)
        => new() { Text = text, FontSize = 13, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    private static TextBlock SectionLabel(string text)
        => new() { Text = text, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Opacity = 0.55, Margin = new Thickness(0, 6, 0, 0) };

    private FrameworkElement ConflictRow(Brush color, string title, string time, bool winner)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var dot = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = color, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 8, 0) };
        Grid.SetColumn(dot, 0);
        var stack = new StackPanel { Spacing = 1 };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(new TextBlock { Text = time, FontSize = 11, Opacity = 0.7 });
        Grid.SetColumn(stack, 1);
        g.Children.Add(dot); g.Children.Add(stack);

        var accent = ((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color;
        return new Border
        {
            Padding = new Thickness(10, 8, 10, 8), CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = winner ? new SolidColorBrush(accent) : (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(winner ? 2 : 1),
            Opacity = winner ? 1.0 : 0.8,
            Child = g
        };
    }

    private static Button ChoiceButton(string text, bool selected)
    {
        var b = new Button { Content = text, MinWidth = 0, Padding = new Thickness(12, 6, 12, 6) };
        if (selected) b.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        return b;
    }

    private static string FormatDuration(TimeSpan d)
    {
        int h = (int)d.TotalHours, m = d.Minutes;
        if (h > 0 && m > 0) return $"{h} h {m} min";
        if (h > 0) return $"{h} h";
        return $"{m} min";
    }

    private static string ShortDay(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "Lun", DayOfWeek.Tuesday => "Mar", DayOfWeek.Wednesday => "Mié",
        DayOfWeek.Thursday => "Jue", DayOfWeek.Friday => "Vie", DayOfWeek.Saturday => "Sáb", _ => "Dom"
    };

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

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
