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
    private string? _viewedPhaseName;   // fase elegida en el selector (null = automática por fecha) (#46)
    private bool _loadingPhaseSel;
    private IReadOnlyList<CalendarEvent> _calEvents = [];   // eventos del calendario de la semana mostrada (#112)
    private DateOnly _weekStart = MondayOf(DateOnly.FromDateTime(DateTime.Now));   // lunes de la semana mostrada (#113)

    private static DateOnly MondayOf(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));

    // Geometría de la rejilla (debe coincidir con BuildGrid).
    private const double HourColWidth = 64;
    private const double DayColMinWidth = 150;   // ancho mínimo de columna de día (#117)
    private double _dayColWidth = DayColMinWidth; // ancho actual: se estira para llenar el ancho visible
    private const double HourHeight = 52;     // alto de UNA hora (constante; antes 2 slots de 26) #61
    // Granularidad de la rejilla de fondo (líneas-guía). Se recalcula en cada Build
    // desde ViewConfig.GranularityMinutes. Los bloques NO dependen de esto: se
    // posicionan por su minuto real (proporcional a HourHeight). #61
    private int _granularity = 60;
    private int _slotsPerHour = 1;            // = 60 / _granularity
    private double _slotHeight = HourHeight;   // = HourHeight / _slotsPerHour

    private enum DragMode { None, Move, ResizeH, ResizeV, ResizeBoth }

    // Estado unificado del arrastre (mover / redimensionar) #82/#88/#89/#90.
    private DragMode _mode = DragMode.None;
    private SessionCard? _card;
    private Ritmo.Core.Scheduling.SessionGroup? _group;
    private double _startX, _startY;          // inicio del puntero en coords de GridRoot
    private double _startTopPx, _startHeightPx;  // top/alto en píxel del bloque al empezar (#61)
    private int _c0, _startSlot, _startDaySpan, _startRowSpan;
    private bool _movedEnough;
    private int _maxDaySpan, _maxRows;         // topes para no solapar (calculados al empezar)
    private int _curCol, _curRow, _curDaySpan, _curRowSpan;   // preview actual (válido)
    private int _curSlotDelta;                 // desplazamiento vertical actual en slots (#61)
    private int _startHour = 8;
    private IReadOnlyList<StudySession> _keptForDrag = [];

    // Arrastre de sesiones provisionales (one-off): estado propio (#126). Y → hora, X → día.
    private OneOffSession? _dragOne;
    private Border? _oneCard;
    private double _oneStartX, _oneStartY, _oneStartTopPx;
    private int _oneStartDayCol, _oneCurDayCol, _oneCurSlotDelta;
    private bool _oneMoved;

    // Panel de detalle y resolución de solapamientos (#114).
    private IReadOnlyList<StudySession> _sessions = [];        // sesiones de la fase visible (para detectar conflictos)
    private IReadOnlyList<OverlapPriority> _priorities = [];   // decisiones de prioridad guardadas
    private IReadOnlyList<OneOffSession> _oneOffs = [];        // sesiones provisionales (con fecha) (#103)
    private IReadOnlyList<StudyNote> _notes = [];             // notas (post-its de sesión, #73)
    private string? _selectedSessionKey;                       // sesión resaltada en la rejilla
    private string? _selectedEventKey;                         // evento resaltado en la rejilla
    private SessionGroup? _selectedGroup;                      // grupo mostrado en el panel
    private CalendarEvent? _selectedEvent;                     // evento mostrado en el panel
    private OneOffSession? _selectedOneOff;                    // sesión provisional mostrada en el panel (#132)

    // Copiar/pegar (#132): portapapeles en memoria + última celda bajo el ratón.
    private StudySession? _clipSession;                        // datos de la sesión copiada
    private bool _clipWasOneOff;                               // ¿la copiada era provisional?
    private int _hoverCol = -1;                                // columna de día bajo el ratón (0..6) o -1
    private TimeOnly _hoverStart;                              // hora (ajustada a la rejilla) bajo el ratón
    private bool _hoverValid;                                  // ¿hay una celda válida bajo el ratón?

    public SchedulePage()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshWeek();
        // El movimiento/soltar se atienden a nivel de rejilla (con el puntero capturado).
        GridRoot.PointerMoved += GridRoot_PointerMoved;
        GridRoot.PointerReleased += GridRoot_PointerReleased;
        GridRoot.PointerCaptureLost += (_, _) => FinishDrag();   // confirma el movimiento al perder la captura (#127)

        // Copiar/pegar sesiones con Ctrl+C / Ctrl+V (#132): copia la seleccionada, pega en la
        // celda del ratón si está libre (tamaño de un día), reusando la geometría del arrastre.
        var copyAcc = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.C, Modifiers = Windows.System.VirtualKeyModifiers.Control };
        copyAcc.Invoked += (_, a) => { a.Handled = true; CopySelectedSession(); };
        var pasteAcc = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.V, Modifiers = Windows.System.VirtualKeyModifiers.Control };
        pasteAcc.Invoked += (_, a) => { a.Handled = true; PasteSessionAtHover(); };
        // Supr: borra la sesión seleccionada (recurrente o provisional) del calendario (#134).
        var deleteAcc = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.Delete };
        deleteAcc.Invoked += (_, a) => { a.Handled = true; DeleteSelected(); };
        KeyboardAccelerators.Add(copyAcc);
        KeyboardAccelerators.Add(pasteAcc);
        KeyboardAccelerators.Add(deleteAcc);

        // Indicador de "ahora" reactivo: se recoloca cada 30 s mientras la página vive. #115
        _nowTimer = DispatcherQueue.CreateTimer();
        _nowTimer.Interval = TimeSpan.FromSeconds(30);
        _nowTimer.Tick += (_, _) => OnNowTick();
        Loaded += (_, _) => _nowTimer.Start();
        Unloaded += (_, _) => _nowTimer.Stop();
    }

    // --- Arrastre: el ScrollViewer roba la captura del puntero (#127). Se desactiva su scroll
    // mientras dura el arrastre y el cierre se confirma en FinishDrag (lo llaman PointerReleased
    // Y PointerCaptureLost, porque este último se dispara primero al soltar). ---
    /// <summary>Desactiva el desplazamiento del ScrollViewer para que no robe la captura durante el arrastre.</summary>
    private void SuppressScroll()
    {
        try
        {
            ScrollHost.VerticalScrollMode = ScrollMode.Disabled;
            ScrollHost.HorizontalScrollMode = ScrollMode.Disabled;
        }
        catch { }
    }

    private void RestoreScroll()
    {
        try
        {
            ScrollHost.VerticalScrollMode = ScrollMode.Auto;
            ScrollHost.HorizontalScrollMode = ScrollMode.Auto;
        }
        catch { }
    }

    private void GridRoot_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Arrastre de una sesión provisional (one-off): preview por píxel (X = día, Y = hora). #126
        if (_dragOne is not null && _oneCard is not null)
        {
            var p = e.GetCurrentPoint(GridRoot).Position;
            if (!_oneMoved && (Math.Abs(p.X - _oneStartX) > 6 || Math.Abs(p.Y - _oneStartY) > 6))
            {
                _oneMoved = true; _oneCard.Opacity = 0.6;
            }
            if (!_oneMoved) return;
            int dDelta = (int)Math.Round((p.X - _oneStartX) / _dayColWidth);
            int sDelta = ScheduleGeometry.PixelsToSlots(p.Y - _oneStartY, HourHeight, _granularity);
            int col = Math.Clamp(_oneStartDayCol + dDelta, 0, 6);
            _oneCurDayCol = col; _oneCurSlotDelta = sDelta;
            Grid.SetColumn(_oneCard, col + 1);
            _oneCard.Margin = new Thickness(2, _oneStartTopPx + sDelta * _slotHeight + 1.5, 2, 0);
            return;
        }

        // Sin arrastre: recuerda la celda (día/hora) bajo el ratón para pegar con Ctrl+V (#132).
        if (_mode == DragMode.None) UpdateHoverCell(e.GetCurrentPoint(GridRoot).Position);

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
            int targetDay = (int)Math.Floor((pt.X - HourColWidth) / _dayColWidth);
            targetDay = Math.Clamp(targetDay, _c0, _c0 + _maxDaySpan - 1);
            _curDaySpan = targetDay - _c0 + 1;
            Grid.SetColumnSpan(_card, _curDaySpan);
        }
        if (_mode is DragMode.ResizeV or DragMode.ResizeBoth)
        {
            // Alto en píxel desde el top del bloque hasta el puntero; ajustado a slots. #61
            double heightPx = (pt.Y - 32) - _startTopPx;   // 32 = cabecera
            int span = Math.Clamp(ScheduleGeometry.PixelsToSlots(heightPx, HourHeight, _granularity), 1, _maxRows);
            _curRowSpan = span;
            _card.Height = Math.Max(14, span * _slotHeight - 3);
        }
        if (_mode == DragMode.Move)
        {
            int dayDelta = (int)Math.Round((pt.X - _startX) / _dayColWidth);
            int slotDelta = ScheduleGeometry.PixelsToSlots(pt.Y - _startY, HourHeight, _granularity);
            int dayIndex = Math.Clamp(_c0 + dayDelta, 0, 7 - _startDaySpan);
            // Solo aceptar la nueva posición si NO solapa (si solapa, se queda en la última válida).
            if (!MoveCollides(dayIndex, slotDelta))
            {
                _curCol = dayIndex + 1; _curSlotDelta = slotDelta;
                Grid.SetColumn(_card, _curCol);
                // Mantiene el offset irregular del bloque y lo desplaza por slots completos.
                _card.Margin = new Thickness(2, _startTopPx + slotDelta * _slotHeight + 1.5, 2, 0);
            }
        }
    }

    private bool MoveCollides(int dayIndex, int slotDelta)
    {
        var rep = _group!.Representative;
        var start = ScheduleMath.ShiftStart(rep.Start, slotDelta, _granularity);
        var cand = _group.Members.Select(m =>
            rep with { Day = Days[Math.Clamp(Array.IndexOf(Days, m.Day) + (dayIndex - _c0), 0, 6)], Start = start });
        return Collides(cand, _keptForDrag);
    }

    // ---------- Copiar / pegar sesiones con Ctrl+C / Ctrl+V (#132) ----------

    /// <summary>Convierte una posición en GridRoot en (día, hora) de la rejilla, ajustada a la granularidad.</summary>
    private void UpdateHoverCell(Windows.Foundation.Point p)
    {
        int col = (int)Math.Floor((p.X - HourColWidth) / _dayColWidth);
        double yRel = p.Y - 32;   // 32 = fila de cabecera
        if (col < 0 || col > 6 || yRel < 0) { _hoverValid = false; _hoverCol = -1; return; }
        int slot = (int)Math.Floor(yRel / _slotHeight);
        int mins = _startHour * 60 + slot * _granularity;
        if (mins < 0 || mins >= 24 * 60) { _hoverValid = false; _hoverCol = -1; return; }
        _hoverCol = col;
        _hoverStart = new TimeOnly(mins / 60, mins % 60);
        _hoverValid = true;
    }

    /// <summary>Ctrl+C: copia la sesión seleccionada (recurrente o provisional) al portapapeles interno.</summary>
    private void CopySelectedSession()
    {
        if (_selectedGroup is not null) { _clipSession = _selectedGroup.Representative; _clipWasOneOff = false; }
        else if (_selectedOneOff is not null) { _clipSession = _selectedOneOff.AsSession(); _clipWasOneOff = true; }
        // Sin nada seleccionado: no hace nada.
    }

    /// <summary>Supr: borra del calendario la sesión seleccionada (recurrente o provisional). #134</summary>
    private void DeleteSelected()
    {
        if (_selectedOneOff is not null)
        {
            AppState.Config.RemoveOneOffSession(_selectedOneOff.Id);
            CloseDetail();   // cierra el panel + repinta
            return;
        }
        if (_selectedGroup is not null && _activePhaseName is not null)
        {
            var phase = AppState.Load().Plan.Phases.FirstOrDefault(p => p.Name == _activePhaseName);
            if (phase is null) return;
            var rep = _selectedGroup.Representative;
            var groupDays = _selectedGroup.Members.Select(m => m.Day).ToHashSet();
            bool Belongs(StudySession x) => SameBlock(x, rep) && groupDays.Contains(x.Day);
            var kept = phase.Schedule.Sessions.Where(x => !Belongs(x)).ToList();
            AppState.Config.ReplaceSessions(_activePhaseName, kept);
            CloseDetail();
        }
        // Un evento de calendario externo (_selectedEvent) no se borra desde aquí.
    }

    /// <summary>
    /// Ctrl+V: pega la sesión copiada en la celda (día/hora) bajo el ratón, SOLO si está libre.
    /// La copia es de UN solo día; según dónde se pegue cambia su día/fecha y su hora (reusa la
    /// misma geometría que el arrastre). Conserva tipo: recurrente→recurrente, provisional→provisional.
    /// </summary>
    private void PasteSessionAtHover()
    {
        if (_clipSession is null || !_hoverValid || _hoverCol < 0) return;
        var src = _clipSession;
        int col = _hoverCol;
        var start = _hoverStart;
        var targetDate = _weekStart.AddDays(col);

        var candidate = src with { Day = Days[col], Start = start };

        // Hueco libre: recurrentes de la fase visible (mismo día) + provisionales de esa fecha.
        var sameDayRecurring = _sessions.Where(s => s.Day == Days[col]).ToList();
        var sameDayOneOffs = _oneOffs.Where(o => o.Date == targetDate).Select(o => o.AsSession()).ToList();
        if (Collides([candidate], [.. sameDayRecurring, .. sameDayOneOffs])) return;   // ocupado → no pega

        if (!_clipWasOneOff && _activePhaseName is not null)
            AppState.Config.AddSession(_activePhaseName, candidate);
        else
            AppState.Config.AddOneOffSession(targetDate, src.Title, start, src.Duration, src.CategoryId, src.PreAlerts, src.IsTentative);

        Build();
    }

    private void GridRoot_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        try { GridRoot.ReleasePointerCapture(e.Pointer); } catch { }
        FinishDrag();
    }

    /// <summary>
    /// Cierra el arrastre confirmando el movimiento/redimensión con los últimos valores del preview.
    /// Idempotente: lo llaman TANTO PointerReleased COMO PointerCaptureLost. En la práctica, al soltar
    /// el ScrollViewer dispara PointerCaptureLost ANTES que PointerReleased; antes eso ejecutaba
    /// CancelDrag y se perdía el movimiento (mode pasaba a None). Ahora cualquiera de los dos confirma. #127
    /// </summary>
    private void FinishDrag()
    {
        RestoreScroll();

        // Sesión provisional (one-off). #126
        if (_dragOne is not null)
        {
            var one = _dragOne; bool oneMoved = _oneMoved; var oc = _oneCard;
            int col = _oneCurDayCol, sDelta = _oneCurSlotDelta;
            _dragOne = null; _oneCard = null;
            if (oc is not null) oc.Opacity = one.IsTentative ? 0.6 : 1.0;
            if (oneMoved) ApplyOneOffMove(one, col, sDelta);   // el clic (sin mover) lo abre Tapped
            return;
        }

        // Sesión recurrente (grupo).
        if (_mode == DragMode.None || _card is null || _group is null) return;
        var mode = _mode; var group = _group; bool moved = _movedEnough;
        int curCol = _curCol, curSlotDelta = _curSlotDelta, c0 = _c0, curDaySpan = _curDaySpan, curRowSpan = _curRowSpan;
        if (_card is not null) _card.Opacity = group.Representative.IsTentative ? 0.6 : 1.0;
        _mode = DragMode.None; _card = null; _group = null;
        if (!moved) return;   // clic sin arrastre -> lo abre Tapped

        switch (mode)
        {
            case DragMode.Move:
                _ = ApplyMove(group, (curCol - 1) - c0, curSlotDelta); break;
            case DragMode.ResizeH:
                _ = ApplyResize(group, c0, curDaySpan); break;
            case DragMode.ResizeV:
                _ = ApplyVerticalResize(group, curRowSpan); break;
            case DragMode.ResizeBoth:
                _ = ApplyResizeBoth(group, c0, curDaySpan, curRowSpan); break;
        }
    }

    /// <summary>Empieza un arrastre desde la tarjeta, según la zona pulsada (borde/cuerpo).</summary>
    private void BeginDrag(SessionCard card, Ritmo.Core.Scheduling.SessionGroup group, DragMode mode,
                           int c0, int startSlot, int daySpan, int rowSpan,
                           Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _mode = mode; _card = card; _group = group; _movedEnough = false;
        _c0 = c0; _startSlot = startSlot; _startDaySpan = daySpan; _startRowSpan = rowSpan;
        _curCol = c0 + 1; _curRow = startSlot; _curDaySpan = daySpan; _curRowSpan = rowSpan; _curSlotDelta = 0;
        var pt = e.GetCurrentPoint(GridRoot).Position; _startX = pt.X; _startY = pt.Y;
        // Top/alto en píxel del bloque (preview por píxel manteniendo su minuto real). #61
        _startTopPx = ScheduleGeometry.TopPixels(group.Representative.Start, _startHour, HourHeight);
        _startHeightPx = ScheduleGeometry.HeightPixels(group.Representative.Duration, HourHeight);

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
        SuppressScroll();   // que el ScrollViewer no robe la captura (#127)
    }

    /// <summary>Empieza a arrastrar una sesión provisional (#126). X = día, Y = hora.</summary>
    private void BeginOneOffDrag(Border card, OneOffSession one, int dayCol, double topPx,
                                 Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_mode != DragMode.None) return;   // no interferir con el arrastre de una recurrente
        _dragOne = one; _oneCard = card; _oneMoved = false;
        _oneStartDayCol = dayCol; _oneStartTopPx = topPx;
        _oneCurDayCol = dayCol; _oneCurSlotDelta = 0;
        var pt = e.GetCurrentPoint(GridRoot).Position; _oneStartX = pt.X; _oneStartY = pt.Y;
        GridRoot.CapturePointer(e.Pointer);
        SuppressScroll();   // que el ScrollViewer no robe la captura (#127)
    }

    /// <summary>Persiste el movimiento de una provisional: nueva fecha (por el día) + hora. #126</summary>
    private void ApplyOneOffMove(OneOffSession one, int dayCol, int slotDelta)
    {
        var newDate = _weekStart.AddDays(Math.Clamp(dayCol, 0, 6));
        var newStart = ScheduleMath.ShiftStart(one.Start, slotDelta, _granularity);
        if (newDate == one.Date && newStart == one.Start) { Build(); return; }   // no se movió de verdad
        // No hay UpdateOneOff en la fachada: se quita y se vuelve a crear con la nueva posición.
        AppState.Config.RemoveOneOffSession(one.Id);
        AppState.Config.AddOneOffSession(newDate, one.Title, newStart, one.Duration, one.CategoryId, one.PreAlerts, one.IsTentative);
        Build();
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
        int slotMin = _granularity;
        return Math.Max(1, (maxEnd - startMin) / slotMin);
    }

    private void Build()
    {
        var settings = AppState.Load();

        // Granularidad de la rejilla de fondo (#61): solo cambia las líneas-guía.
        _granularity = ScheduleGeometry.NormalizeGranularity(settings.ViewConfig.GranularityMinutes);
        _slotsPerHour = ScheduleGeometry.SlotsPerHour(_granularity);
        _slotHeight = ScheduleGeometry.SlotHeight(HourHeight, _granularity);

        // Colores por categoría de bloque (#45/#83).
        Services.ScheduleColors.SetCategories(settings.Categories);

        var today = DateOnly.FromDateTime(DateTime.Now);
        _builtDate = today;
        // La fase mostrada se resuelve por la SEMANA visible, no por "hoy" (#46 fix):
        // si una fase cubre algún día de la semana, se usa; si no, no hay fase (rejilla
        // vacía) — así al pasar el límite de una fase, el horario "corta". Una fase
        // elegida a mano en el selector tiene prioridad.
        SchedulePhase? phase = _viewedPhaseName is not null
            ? settings.Plan.Phases.FirstOrDefault(p => p.Name == _viewedPhaseName)
            : null;
        if (phase is null)
            for (int d = 0; d < 7 && phase is null; d++)
                phase = settings.Plan.GetActivePhase(_weekStart.AddDays(d));
        BuildPhaseSelector(settings, phase);
        var schedule = phase?.Schedule ?? new WeeklySchedule();   // sin fase -> rejilla vacía
        _activePhaseName = phase?.Name;
        AddBtn.IsEnabled = phase is not null;

        PhaseInfo.Text = phase is not null
            ? $"{phase.Name}  ·  {phase.ValidFrom:dd/MM/yyyy} → {(phase.ValidTo?.ToString("dd/MM/yyyy") ?? "indefinida")}"
            : settings.Plan.Phases.Count > 0
                ? "Sin fase para esta semana"
                : "Sin fase configurada";

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
        _oneOffs = settings.OneOffSessions;
        _notes = settings.Notes;

        // Extiende el rango si una sesión provisional de esta semana cae fuera (#103).
        foreach (var o in _oneOffs)
        {
            if (o.Date < _weekStart || o.Date > _weekStart.AddDays(6)) continue;
            startH = Math.Min(startH, o.Start.Hour);
            endH = Math.Max(endH, (int)Math.Ceiling((o.Start.ToTimeSpan() + o.Duration).TotalHours));
        }

        _startHour = startH;
        BuildGrid(schedule, startH, endH);
    }

    private void BuildGrid(WeeklySchedule schedule, int startH, int endH)
    {
        int slotsPerHour = _slotsPerHour;     // según la granularidad elegida (#61)
        double rowHeight = _slotHeight;
        int hours = endH - startH;
        int totalRows = hours * slotsPerHour;

        var g = GridRoot;
        g.Children.Clear();
        g.RowDefinitions.Clear();
        g.ColumnDefinitions.Clear();

        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HourColWidth) });
        for (int c = 0; c < 7; c++)
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_dayColWidth) });

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

        // Líneas-guía de fondo: una celda por SLOT de la granularidad (#61). Uniformes
        // para todos los días; los bloques se pintan luego encima por su minuto real.
        for (int r = 0; r < totalRows; r++)
            for (int c = 0; c < 7; c++)
            {
                var bgCell = new Border
                {
                    BorderBrush = line, BorderThickness = new Thickness(0, 0, 1, 1),
                    Background = c == todayCol ? todayTint : null
                };
                Grid.SetRow(bgCell, 1 + r); Grid.SetColumn(bgCell, c + 1);
                g.Children.Add(bgCell);
            }

        // Etiquetas de hora (solo horas redondas → eje limpio) + botón "+" por hora vacía.
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
            // Posición por píxeles según el minuto REAL (independiente de la granularidad): #61
            double topPx = ScheduleGeometry.TopPixels(s.Start, startH, HourHeight);
            double heightPx = ScheduleGeometry.HeightPixels(s.Duration, HourHeight);
            // Índices de slot (el arrastre se ajusta a la rejilla de la granularidad activa).
            int startSlotIdx = (int)Math.Round(ScheduleGeometry.MinutesFromStart(s.Start, startH) / (double)_granularity);
            int spanSlots = Math.Max(1, (int)Math.Round(s.Duration.TotalMinutes / (double)_granularity));

            var baseColor = ScheduleColors.For(s.CategoryId);
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
                            Foreground = ScheduleColors.TextFor(s.CategoryId), TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock {
                            Text = $"{s.Start:HH\\:mm}–{s.End:HH\\:mm}{(s.IsTentative ? "  (?)" : "")}",
                            FontSize = 10, Opacity = 0.75, Foreground = ScheduleColors.TextFor(s.CategoryId) }
                    }
                }
            };
            var card = new SessionCard
            {
                Content = visual,
                VerticalAlignment = VerticalAlignment.Top,           // flota por su minuto real (#61)
                Height = Math.Max(14, heightPx - 3),
                Margin = new Thickness(2, topPx + 1.5, 2, 0),
                Opacity = s.IsTentative ? 0.6 : 1.0,
                Tag = group
            };

            // Hover: borde de acento + cursor según la zona (cuerpo=mover, bordes/esquina=redimensionar).
            double restThickness = ring ? 2 : 0;
            int cardStartSlot = startSlotIdx, cardSpanSlots = spanSlots, cardDayCol = dayCol;
            var thisGroup = group;
            card.PointerEntered += (_, _) => {
                visual.BorderThickness = new Thickness(1.5);
                visual.BorderBrush = ScheduleColors.TextFor(s.CategoryId);
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
            Grid.SetRow(card, 1); Grid.SetRowSpan(card, totalRows);   // flota; alto/margen lo posicionan (#61)
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
                    Margin = new Thickness(0, topPx + 4, 6, 0),
                    CornerRadius = new CornerRadius(4)
                };
                ToolTipService.SetToolTip(focusBtn, "Concentrarme en este bloque");
                focusBtn.Click += (_, _) => FocusNow();
                Grid.SetRow(focusBtn, 1); Grid.SetRowSpan(focusBtn, totalRows);
                Grid.SetColumn(focusBtn, dayCol + 1); Grid.SetColumnSpan(focusBtn, group.DaySpan);
                g.Children.Add(focusBtn);
            }

            // Badge de post-its: nº de notas de esta sesión (#73).
            int noteCount = _notes.Count(n => string.Equals(n.SessionTitle, s.Title, StringComparison.OrdinalIgnoreCase));
            if (noteCount > 0)
            {
                var badge = new Border
                {
                    Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(5, 0, 5, 1),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, topPx + Math.Max(14, heightPx - 3) - 20, 5, 0), IsHitTestVisible = false,
                    Child = new TextBlock { Text = $"📝 {noteCount}", FontSize = 10 }
                };
                Grid.SetRow(badge, 1); Grid.SetRowSpan(badge, totalRows);
                Grid.SetColumn(badge, dayCol + 1); Grid.SetColumnSpan(badge, group.DaySpan);
                g.Children.Add(badge);
            }
        }

        // Indicador de "ahora" sobre la columna de hoy (línea + punto), reposicionable
        // por el temporizador sin reconstruir la rejilla. #115
        _nowStartH = startH; _nowHours = hours; _nowTodayCol = todayCol; _nowTotalRows = totalRows;
        PlaceNowIndicator();

        OverlayOneOffs(g, startH, hours);    // sesiones provisionales de la semana (#103)
        OverlayCalendar(g, startH, hours);   // eventos del calendario sobre la rejilla (#112)

        ApplyColumnWidths();   // estira las columnas de día para llenar el ancho visible (#117)
    }

    // ---------- Responsive: las columnas de día llenan el ancho visible (#117) ----------

    private void ScrollHost_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyColumnWidths();

    /// <summary>
    /// Ajusta el ancho de las 7 columnas de día para llenar el viewport del scroll
    /// (sin reconstruir la rejilla). Nunca baja del mínimo: si la ventana es estrecha,
    /// se mantiene el mínimo y aparece scroll horizontal.
    /// </summary>
    private void ApplyColumnWidths()
    {
        if (ScrollHost is null || GridRoot.ColumnDefinitions.Count < 8) return;
        double available = ScrollHost.ViewportWidth;
        if (available <= 0) return;

        // -2 de holgura para no provocar un scrollbar horizontal por redondeo.
        double dw = Math.Max(DayColMinWidth, (available - HourColWidth - 2) / 7.0);
        if (Math.Abs(dw - _dayColWidth) < 0.5) return;   // sin cambios apreciables
        _dayColWidth = dw;
        for (int c = 1; c < GridRoot.ColumnDefinitions.Count; c++)
            GridRoot.ColumnDefinitions[c].Width = new GridLength(dw);
    }

    // ---------- Indicador de la hora actual ("ahora"), reactivo (#115) ----------

    private int _nowStartH, _nowHours, _nowTodayCol = -1, _nowTotalRows;     // contexto de la rejilla pintada
    private readonly List<UIElement> _nowParts = new();       // línea + punto actuales en GridRoot
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _nowTimer;
    private DateOnly _builtDate;                               // día con el que se construyó (para detectar cambio de día)

    /// <summary>
    /// Coloca (o recoloca) la línea + punto de la hora actual en la columna de hoy.
    /// Es ligero: solo quita/añade esos 2 elementos, sin reconstruir la rejilla.
    /// Si no estamos en la semana actual o la hora cae fuera del rango, no pinta nada.
    /// </summary>
    private void PlaceNowIndicator()
    {
        foreach (var e in _nowParts) GridRoot.Children.Remove(e);
        _nowParts.Clear();
        if (_nowTodayCol < 0) return;

        var now = DateTime.Now;
        double nowHours = now.Hour + now.Minute / 60.0 + now.Second / 3600.0 - _nowStartH;
        if (nowHours < 0 || nowHours > _nowHours) return;

        // Posición por píxel (proporcional al minuto real, igual que los bloques). #61
        double offset = nowHours * HourHeight;
        var accent = ((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color;
        var brush = new SolidColorBrush(accent);

        var line = new Border
        {
            Height = 2, Background = brush,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, offset, 0, 0),
            IsHitTestVisible = false
        };
        Grid.SetRow(line, 1); Grid.SetColumn(line, _nowTodayCol + 1); Grid.SetRowSpan(line, _nowTotalRows);

        var dot = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = 9, Height = 9, Fill = brush,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(-3, offset - 3.5, 0, 0),
            IsHitTestVisible = false
        };
        Grid.SetRow(dot, 1); Grid.SetColumn(dot, _nowTodayCol + 1); Grid.SetRowSpan(dot, _nowTotalRows);

        GridRoot.Children.Add(line); GridRoot.Children.Add(dot);
        _nowParts.Add(line); _nowParts.Add(dot);
    }

    /// <summary>Tick del temporizador: si cambió el día, reconstruye; si no, recoloca el "ahora".</summary>
    private void OnNowTick()
    {
        if (DateOnly.FromDateTime(DateTime.Now) != _builtDate) RefreshWeek();   // cruzó medianoche
        else PlaceNowIndicator();
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

    // ---------- Selector de fase del plan (ver pasadas/futuras, #46) ----------

    private void BuildPhaseSelector(Ritmo.Core.Persistence.AppSettings settings, SchedulePhase? current)
    {
        _loadingPhaseSel = true;
        PhaseSelector.Items.Clear();
        PhaseSelector.Items.Add(new ComboBoxItem { Content = "Automática (por fecha)", Tag = "" });
        foreach (var p in settings.Plan.OrderedPhases)
            PhaseSelector.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p.Name });

        int idx = 0;
        if (_viewedPhaseName is not null)
            for (int i = 0; i < PhaseSelector.Items.Count; i++)
                if (PhaseSelector.Items[i] is ComboBoxItem it && (string)it.Tag == _viewedPhaseName) { idx = i; break; }
        PhaseSelector.SelectedIndex = idx;
        // Solo tiene sentido elegir si hay más de una fase.
        PhaseSelector.Visibility = settings.Plan.Phases.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        _loadingPhaseSel = false;
    }

    private void PhaseSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPhaseSel) return;
        var tag = (PhaseSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        _viewedPhaseName = string.IsNullOrEmpty(tag) ? null : tag;
        CloseDetail(internalRefresh: true);   // la selección previa puede no aplicar a otra fase
        Build();
    }

    // ---------- Navegador de calendario: saltar por mes/año (#119) ----------

    private void CalNavFlyout_Opening(object sender, object e)
        => CalNav.SetDisplayDate(new DateTimeOffset(_weekStart.ToDateTime(TimeOnly.MinValue)));

    private void CalNav_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs e)
    {
        if (e.AddedDates.Count == 0) return;
        _weekStart = MondayOf(DateOnly.FromDateTime(e.AddedDates[0].DateTime));
        CalNavFlyout.Hide();
        RefreshWeek();
    }

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
        int totalRows = hours * _slotsPerHour;
        double maxPx = hours * HourHeight;
        var accent = ((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color;

        foreach (var ev in _calEvents)
        {
            int dayCol = Array.IndexOf(Days, ev.Start.DayOfWeek);
            if (dayCol < 0) continue;

            var cal = string.IsNullOrEmpty(ev.Calendar) ? "Calendario" : ev.Calendar!;
            var color = CalPalette[CalColorIndex(cal)];   // color estable por nombre de calendario

            // Posición por píxel (igual que los bloques): el evento cae en su minuto real. #61
            double topPx, heightPx;
            bool shortEv;
            if (ev.AllDay) { topPx = 0; heightPx = HourHeight * 0.6; shortEv = true; }
            else
            {
                double startHours = ev.Start.Hour + ev.Start.Minute / 60.0 - startH;
                if (startHours >= hours) continue;
                topPx = Math.Max(0, startHours) * HourHeight;
                heightPx = Math.Max(8, (ev.End - ev.Start).TotalHours * HourHeight);
                if (topPx + heightPx > maxPx) heightPx = Math.Max(8, maxPx - topPx);
                shortEv = heightPx < HourHeight * 0.75;
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
            if (shortEv)
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
                Margin = new Thickness(3, topPx + 1, 3, 0),       // posición por píxel (#61)
                Padding = new Thickness(5, 1, 4, 1),
                VerticalAlignment = VerticalAlignment.Top,
                Height = Math.Max(14, heightPx - 2),
                Opacity = cardOpacity,                            // gana la sesión -> toda la tarjeta recede (#114)
                Child = content                                   // clicable: abre el detalle del evento (#114)
            };
            var captured = ev;
            card.Tapped += (_, _) => ShowEventDetail(captured);
            Grid.SetRow(card, 1); Grid.SetRowSpan(card, totalRows);
            Grid.SetColumn(card, dayCol + 1);
            g.Children.Add(card);
        }
    }

    // ---------- Sesiones provisionales sobre la rejilla (#103) ----------

    private void OverlayOneOffs(Grid g, int startH, int hours)
    {
        if (_oneOffs.Count == 0) return;
        int totalRows = hours * _slotsPerHour;
        double maxPx = hours * HourHeight;
        var weekEnd = _weekStart.AddDays(6);
        var accentBrush = new SolidColorBrush(((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color);

        foreach (var one in _oneOffs)
        {
            if (one.Date < _weekStart || one.Date > weekEnd) continue;
            int dayCol = Array.IndexOf(Days, one.Date.DayOfWeek);
            if (dayCol < 0) continue;

            double startHours = one.Start.Hour + one.Start.Minute / 60.0 - startH;
            if (startHours >= hours) continue;
            double topPx = Math.Max(0, startHours) * HourHeight;                 // posición por píxel (#61)
            double heightPx = Math.Max(8, one.Duration.TotalHours * HourHeight);
            if (topPx + heightPx > maxPx) heightPx = Math.Max(8, maxPx - topPx);

            var end = one.Start.Add(one.Duration);
            var content = new StackPanel { Spacing = 0 };
            content.Children.Add(new TextBlock { Text = one.Title, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = ScheduleColors.TextFor(one.CategoryId), TextTrimming = TextTrimming.CharacterEllipsis });
            content.Children.Add(new TextBlock { Text = $"{one.Start:HH\\:mm}–{end:HH\\:mm}", FontSize = 10, Opacity = 0.75,
                Foreground = ScheduleColors.TextFor(one.CategoryId) });
            content.Children.Add(new TextBlock { Text = "✦ solo esta semana", FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = accentBrush });

            var card = new Border
            {
                Background = ScheduleColors.For(one.CategoryId),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 3, 6, 3),
                VerticalAlignment = VerticalAlignment.Top,
                Height = Math.Max(14, heightPx - 3),
                Margin = new Thickness(2, topPx + 1.5, 2, 0),
                BorderBrush = accentBrush,
                BorderThickness = new Thickness(2),   // borde de acento = distinta de las recurrentes
                Opacity = one.IsTentative ? 0.6 : 1.0,
                Child = content
            };
            var captured = one;
            var capturedCol = dayCol; var capturedTop = topPx;
            card.Tapped += (_, _) => ShowOneOffDetail(captured);
            card.PointerPressed += (_, e) => BeginOneOffDrag(card, captured, capturedCol, capturedTop, e);   // #126
            Grid.SetRow(card, 1); Grid.SetRowSpan(card, totalRows);
            Grid.SetColumn(card, dayCol + 1);
            g.Children.Add(card);
        }
    }

    /// <summary>Detalle de una sesión provisional en el panel, con opción de eliminarla.</summary>
    private void ShowOneOffDetail(OneOffSession one)
    {
        _selectedGroup = null; _selectedEvent = null;
        _selectedSessionKey = null; _selectedEventKey = null;
        _selectedOneOff = one;   // para copiar con Ctrl+C (#132)
        Build();

        var es = new System.Globalization.CultureInfo("es-ES");
        var content = DetailContent;
        content.Children.Clear();
        content.Children.Add(DetailHeader("Sesión provisional"));
        content.Children.Add(TitleRow(ScheduleColors.For(one.CategoryId), one.Title));

        var meta = new StackPanel { Spacing = 4 };
        meta.Children.Add(MetaLine(AppState.Load().CategoryName(one.CategoryId) + (one.IsTentative ? "  ·  provisional" : "")));
        meta.Children.Add(MetaLine(Capitalize(one.Date.ToString("dddd d 'de' MMMM", es))));
        meta.Children.Add(MetaLine($"{one.Start:HH\\:mm} – {one.Start.Add(one.Duration):HH\\:mm}  ·  {FormatDuration(one.Duration)}"));
        meta.Children.Add(MetaLine("Solo esta semana · no se repite"));
        content.Children.Add(meta);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var edit = new Button { Content = "Editar" };
        edit.Click += (_, _) => _ = EditOneOff(one);
        var del = new Button { Content = "Eliminar" };
        del.Click += (_, _) => { AppState.Config.RemoveOneOffSession(one.Id); CloseDetail(); };
        actions.Children.Add(edit); actions.Children.Add(del);
        content.Children.Add(actions);

        DetailPanel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Edita una sesión provisional (#103): permite cambiar título/día/hora/tipo/avisos, y
    /// también reconvertirla en recurrente (desmarcando «solo esta semana»). «Eliminar» la borra.
    /// </summary>
    private async Task EditOneOff(OneOffSession one)
    {
        var dlg = new SessionDialog
        {
            XamlRoot = this.XamlRoot,
            PrimaryButtonText = "Guardar",
            SecondaryButtonText = "Cancelar",
            CloseButtonText = "Eliminar"
        };
        dlg.SetCategories(AppState.Load().Categories);   // categorías dinámicas (#83)
        dlg.SetKnownTitles(AllTitles());
        dlg.LoadFrom(one.AsSession());
        dlg.PreselectDays([one.Date.DayOfWeek]);   // por si se reconvierte a recurrente
        dlg.SetOneOff(true);
        dlg.SetOneOffDates(one.Date, one.Date);    // rango de fechas pre-rellenado (#131)

        var result = await dlg.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            AppState.Config.RemoveOneOffSession(one.Id);   // se reemplaza por lo editado
            if (dlg.IsOneOff || _activePhaseName is null)
                AddOneOffsForRange(dlg);   // provisional: una por cada día del rango (#131)
            else
                foreach (var d in dlg.SelectedDays)
                    AppState.Config.AddSession(_activePhaseName, dlg.ToSession(d));   // reconvertida a recurrente
        }
        else if (result == ContentDialogResult.None)   // Eliminar
            AppState.Config.RemoveOneOffSession(one.Id);
        else
            return;   // Cancelar
        CloseDetail();
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

        // Nueva hora de inicio (mantiene duración), acotada al día. Slot = granularidad activa (#61).
        var newStart = ScheduleMath.ShiftStart(rep.Start, slotDelta, _granularity);

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

        var newDuration = TimeSpan.FromMinutes(Math.Max(1, newSpanRows) * _granularity);
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

        var newDuration = TimeSpan.FromMinutes(Math.Max(1, rowSpan) * _granularity);
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

    private void TaskBehaviorBtn_Click(object sender, RoutedEventArgs e) => ShowTaskBehaviorList();

    private async Task ShowAddDialog(DayOfWeek? day = null, TimeOnly? start = null)
    {
        if (_activePhaseName is null) return;
        var dlg = new SessionDialog { XamlRoot = this.XamlRoot };
        var settings = AppState.Load();
        dlg.SetCategories(settings.Categories);   // categorías dinámicas (#83)
        dlg.SetKnownTitles(AllTitles());
        dlg.LoadDefaults(day, start, settings.ViewConfig.DefaultPreAlertMinutes);   // aviso por defecto configurable (#48)
        // Fecha por defecto del rango provisional: el día pulsado (o el lunes visible). #131
        var defDate = day is { } dd ? _weekStart.AddDays(Math.Max(0, Array.IndexOf(Days, dd))) : _weekStart;
        dlg.SetOneOffDates(defDate, defDate);
        var result = await dlg.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (dlg.IsOneOff)
                AddOneOffsForRange(dlg);   // provisional: una por cada día del rango de fechas (#131)
            else
                foreach (var d in dlg.SelectedDays)   // recurrente: en cada día marcado (#81)
                    AppState.Config.AddSession(_activePhaseName, dlg.ToSession(d));
            Build();
        }
    }

    /// <summary>Crea una sesión provisional por cada día del rango de fechas del diálogo (#131).</summary>
    private void AddOneOffsForRange(SessionDialog dlg)
    {
        for (var date = dlg.StartDate; date <= dlg.EndDate; date = date.AddDays(1))
        {
            var ss = dlg.ToSession(date.DayOfWeek);
            AppState.Config.AddOneOffSession(date, ss.Title, ss.Start, ss.Duration, ss.CategoryId, ss.PreAlerts, ss.IsTentative);
        }
    }

    /// <summary>¿Comparten título/tipo/horario/provisional? (para identificar el grupo fusionado).</summary>
    private static bool SameBlock(StudySession a, StudySession b)
        => a.Title.Trim() == b.Title.Trim() && a.CategoryId == b.CategoryId
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
        dlg.SetCategories(AppState.Load().Categories);   // categorías dinámicas (#83)
        dlg.SetKnownTitles(AllTitles());
        dlg.LoadFrom(rep);
        dlg.PreselectDays(groupDays);   // todos los días del grupo marcados
        dlg.SetOneOffDates(_weekStart, _weekStart);   // por si se convierte a provisional (#131)

        var result = await dlg.ShowAsync();

        // Las sesiones que NO son de este grupo se conservan tal cual.
        var kept = phase.Schedule.Sessions.Where(x => !Belongs(x)).ToList();

        if (result == ContentDialogResult.Primary)
        {
            if (dlg.IsOneOff)
            {
                // Convertir a extraordinaria (#103/#131): quitar el grupo recurrente y crear una
                // sesión provisional por cada día del rango de fechas elegido.
                AppState.Config.ReplaceSessions(_activePhaseName, kept);
                AddOneOffsForRange(dlg);
            }
            else
            {
                // Reemplaza el grupo por una sesión recurrente en cada día marcado.
                var rebuilt = dlg.SelectedDays.Select(d => dlg.ToSession(d));
                AppState.Config.ReplaceSessions(_activePhaseName, [.. kept, .. rebuilt]);
            }
        }
        else if (result == ContentDialogResult.None)   // Eliminar todo el grupo
            AppState.Config.ReplaceSessions(_activePhaseName, kept);
        else
            return; // Cancelar
        Build();
    }

    // ---------- Panel lateral de detalle + resolución de solapamientos (#114) ----------

    /// <summary>Títulos únicos del horario (todas las fases + suelto) para sugerir en el diálogo. #116</summary>
    private static IReadOnlyList<string> AllTitles()
    {
        var s = AppState.Load();
        return SessionGrouping.AllTitles(s.Plan, s.Schedule);
    }

    /// <summary>Identidad estable de una sesión, para resaltarla tras repintar la rejilla.</summary>
    private static string SessionKey(StudySession s)
        => $"{s.Title.Trim()}|{s.CategoryId}|{s.Start}|{s.Duration}|{s.IsTentative}";

    /// <summary>Muestra el detalle completo de una sesión (y sus solapamientos) en el panel.</summary>
    private void ShowSessionDetail(SessionGroup group)
    {
        var rep = group.Representative;
        _selectedGroup = group; _selectedEvent = null; _selectedOneOff = null;
        _selectedSessionKey = SessionKey(rep); _selectedEventKey = null;
        Build();   // repinta la rejilla con el resaltado

        var content = DetailContent;
        content.Children.Clear();
        content.Children.Add(DetailHeader("Detalle de la sesión"));
        content.Children.Add(TitleRow(ScheduleColors.For(rep.CategoryId), rep.Title));

        var meta = new StackPanel { Spacing = 4 };
        meta.Children.Add(MetaLine(AppState.Load().CategoryName(rep.CategoryId) + (rep.IsTentative ? "  ·  provisional" : "")));
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

        // Atajo: qué apps/enlaces del entorno se abren para este tipo de sesión (#116).
        var behaviorBtn = new HyperlinkButton { Content = "Qué se abre al concentrarme…", Padding = new Thickness(0) };
        behaviorBtn.Click += async (_, _) => await ShowSessionBehavior(rep.Title);
        content.Children.Add(behaviorBtn);

        AppendSessionNotes(rep.Title);   // post-its de la sesión (#73)

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
        _selectedEvent = ev; _selectedGroup = null; _selectedOneOff = null;
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
            box.Children.Add(ConflictRow(ScheduleColors.For(sess.CategoryId), $"Sesión · {sess.Title}",
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

    // ---------- Notas como post-it de la sesión (#73) ----------

    private void AppendSessionNotes(string sessionTitle)
    {
        var content = DetailContent;
        content.Children.Add(SectionLabel("NOTAS"));
        var notes = _notes
            .Where(n => string.Equals(n.SessionTitle, sessionTitle, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Order).ToList();
        if (notes.Count == 0)
            content.Children.Add(new TextBlock { Text = "Sin notas para esta sesión.", Opacity = 0.55, FontSize = 12 });
        foreach (var note in notes) content.Children.Add(SessionNoteCard(note));

        var add = new HyperlinkButton { Content = "+ Añadir nota", Padding = new Thickness(0) };
        add.Click += async (_, _) => await AddSessionNote(sessionTitle);
        content.Children.Add(add);
    }

    private FrameworkElement SessionNoteCard(StudyNote note)
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var t = new TextBlock { Text = note.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(t, 0);
        var edit = IconButton(Symbol.Edit, "Editar nota", () => _ = EditSessionNote(note));
        Grid.SetColumn(edit, 1);
        var del = IconButton(Symbol.Delete, "Eliminar nota", () => RemoveSessionNote(note));
        Grid.SetColumn(del, 2);
        header.Children.Add(t); header.Children.Add(edit); header.Children.Add(del);

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(header);
        if (!string.IsNullOrWhiteSpace(note.Content))
        {
            var md = MarkdownRenderer.Build(note.Content);
            md.Opacity = 0.85;
            stack.Children.Add(md);
        }
        return new Border
        {
            Padding = new Thickness(10, 8, 6, 8), CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], BorderThickness = new Thickness(1),
            Child = stack
        };
    }

    private static Button IconButton(Symbol symbol, string tooltip, Action onClick)
    {
        var b = new Button
        {
            Content = new SymbolIcon(symbol) { },
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0), Padding = new Thickness(6, 2, 6, 2),
            MinWidth = 0, VerticalAlignment = VerticalAlignment.Top
        };
        ToolTipService.SetToolTip(b, tooltip);
        b.Click += (_, _) => onClick();
        return b;
    }

    private async Task AddSessionNote(string sessionTitle)
    {
        var dlg = new NoteDialog { XamlRoot = this.XamlRoot };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary && dlg.TitleText.Length > 0)
        {
            AppState.Config.AddNote(dlg.TitleText, dlg.ContentText, sessionTitle: sessionTitle);
            if (_selectedGroup is not null) ShowSessionDetail(_selectedGroup);
        }
    }

    private async Task EditSessionNote(StudyNote note)
    {
        var dlg = new NoteDialog { XamlRoot = this.XamlRoot };
        dlg.LoadFrom(note);
        if (await dlg.ShowAsync() == ContentDialogResult.Primary && dlg.TitleText.Length > 0)
        {
            AppState.Config.UpdateNote(note.Id, dlg.TitleText, dlg.ContentText);
            if (_selectedGroup is not null) ShowSessionDetail(_selectedGroup);
        }
    }

    private void RemoveSessionNote(StudyNote note)
    {
        AppState.Config.RemoveNote(note.Id);
        if (_selectedGroup is not null) ShowSessionDetail(_selectedGroup);
    }

    /// <summary>Abre el diálogo "qué se abre al concentrarme" para un tipo de sesión (#116).</summary>
    private async Task ShowSessionBehavior(string title)
    {
        var dlg = new SessionBehaviorDialog { XamlRoot = this.XamlRoot };
        dlg.Configure(title);
        if (await dlg.ShowAsync() == ContentDialogResult.Primary) dlg.Apply();
    }

    /// <summary>Lista agrupada de tipos de sesión (por título) en el panel, para configurar cada uno (#116).</summary>
    private void ShowTaskBehaviorList()
    {
        _selectedGroup = null; _selectedEvent = null; _selectedOneOff = null;
        _selectedSessionKey = null; _selectedEventKey = null;
        Build();   // quita resaltado de la rejilla

        var content = DetailContent;
        content.Children.Clear();
        content.Children.Add(DetailHeader("Comportamiento por tarea"));
        content.Children.Add(new TextBlock
        {
            Text = "Por cada tipo de sesión (agrupado por título) elige qué apps y enlaces del entorno se abren al concentrarte.",
            Opacity = 0.7, FontSize = 12, TextWrapping = TextWrapping.Wrap
        });

        var titles = AllTitles();
        if (titles.Count == 0)
            content.Children.Add(new TextBlock { Text = "Aún no hay sesiones en el horario.", Opacity = 0.6, FontSize = 13, Margin = new Thickness(0, 6, 0, 0) });

        foreach (var title in titles)
        {
            var t = title;
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(name, 0);
            var btn = new Button { Content = "Configurar" };
            btn.Click += async (_, _) => await ShowSessionBehavior(t);
            Grid.SetColumn(btn, 1);
            row.Children.Add(name); row.Children.Add(btn);
            content.Children.Add(row);
        }

        DetailPanel.Visibility = Visibility.Visible;
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
        _selectedGroup = null; _selectedEvent = null; _selectedOneOff = null;
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
