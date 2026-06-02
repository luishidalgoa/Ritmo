using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Ritmo.Core.Persistence;
using Ritmo_App.Services;
using Windows.Graphics;

namespace Ritmo_App;

/// <summary>
/// Isla flotante de concentración (#118): ventana pequeña, siempre visible, estética "modo reposo
/// (StandBy)" de iPhone. Muestra la hora + el cronómetro Pomodoro y permite pausar/saltar/abrir.
///
/// #152: se puede ARRASTRAR (recuerda su posición) y tiene dos tamaños — normal y compacto (recuerda
/// la elección). No posee el motor: la actualiza y la controla <see cref="TimerPage"/> vía <see cref="UpdateView"/>.
/// </summary>
public sealed partial class FocusOverlayWindow : Window
{
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);

    public event Action? ExpandRequested;
    public event Action? PauseResumeRequested;
    public event Action? SkipRequested;

    private bool _compact;

    public FocusOverlayWindow()
    {
        InitializeComponent();
        Title = "Ritmo — foco";

        AppWindow.IsShownInSwitchers = false;     // fuera del Alt+Tab / barra de tareas
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.IsAlwaysOnTop = true;
            p.IsResizable = false;
            p.IsMaximizable = false;
            p.IsMinimizable = false;
            p.SetBorderAndTitleBar(true, false);  // borde (esquinas redondeadas Win11) sin barra de título
        }

        AttachDrag(NormalDragArea);
        AttachDrag(CompactDragArea);

        var s = AppState.Load();
        SetCompact(s.IslandCompact, save: false);   // tamaño + visibilidad según lo recordado
        RestorePosition(s);                          // posición recordada o esquina superior derecha
    }

    private double Scale()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double s = GetDpiForWindow(hwnd) / 96.0;
        return s <= 0 ? 1.0 : s;
    }

    /// <summary>Aplica el tamaño/visibilidad del modo (normal/compacto) y, si procede, lo recuerda.</summary>
    private void SetCompact(bool compact, bool save)
    {
        _compact = compact;
        NormalRoot.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactRoot.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;

        double scale = Scale();
        int w = (int)((compact ? 300 : 452) * scale);
        int h = (int)((compact ? 92 : 208) * scale);
        AppWindow.Resize(new SizeInt32(w, h));   // mantiene la esquina superior izquierda

        EnsureOnScreen();
        if (save) SavePlacement();
    }

    private void RestorePosition(AppSettings s)
    {
        if (s.IslandLeft is int x && s.IslandTop is int y)
        {
            AppWindow.Move(new PointInt32(x, y));
            EnsureOnScreen();
        }
        else PositionTopRight();
    }

    /// <summary>Coloca la isla en la esquina superior derecha (posición por defecto), con su tamaño actual.</summary>
    private void PositionTopRight()
    {
        double scale = Scale();
        var size = AppWindow.Size;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var wa = area.WorkArea;
        int margin = (int)(18 * scale);
        AppWindow.Move(new PointInt32(wa.X + wa.Width - size.Width - margin, wa.Y + margin));
    }

    /// <summary>Mantiene la isla dentro del área visible del monitor más cercano.</summary>
    private void EnsureOnScreen()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        var wa = area.WorkArea;
        var pos = AppWindow.Position;
        var size = AppWindow.Size;
        int x = Math.Clamp(pos.X, wa.X, Math.Max(wa.X, wa.X + wa.Width - size.Width));
        int y = Math.Clamp(pos.Y, wa.Y, Math.Max(wa.Y, wa.Y + wa.Height - size.Height));
        if (x != pos.X || y != pos.Y) AppWindow.Move(new PointInt32(x, y));
    }

    private void SavePlacement()
    {
        var pos = AppWindow.Position;
        AppState.Config.SetIslandPlacement(pos.X, pos.Y, _compact);
    }

    // ---------- Arrastre (#152): mover la ventana siguiendo el cursor (coords de pantalla) ----------

    private bool _dragging;
    private POINT _dragCursor;
    private PointInt32 _dragWindow;

    private void AttachDrag(UIElement handle)
    {
        handle.PointerPressed += (s, e) =>
        {
            GetCursorPos(out _dragCursor);
            _dragWindow = AppWindow.Position;
            _dragging = true;
            ((UIElement)s).CapturePointer(e.Pointer);
        };
        handle.PointerMoved += (_, _) =>
        {
            if (!_dragging) return;
            GetCursorPos(out var cur);
            AppWindow.Move(new PointInt32(_dragWindow.X + (cur.X - _dragCursor.X), _dragWindow.Y + (cur.Y - _dragCursor.Y)));
        };
        handle.PointerReleased += (s, e) =>
        {
            if (!_dragging) return;
            _dragging = false;
            ((UIElement)s).ReleasePointerCapture(e.Pointer);
            EnsureOnScreen();
            SavePlacement();
        };
        handle.PointerCaptureLost += (_, _) =>
        {
            if (!_dragging) return;
            _dragging = false;
            SavePlacement();
        };
    }

    /// <summary>Refresca los textos/progreso (lo llama TimerPage en cada tick), en ambos modos.</summary>
    public void UpdateView(string clock, string date, string phaseLabel, string pomo,
                           double progress, bool isRunning, bool canSkip)
    {
        ClockText.Text = clock;
        CompactClock.Text = clock;
        DateText.Text = date;
        PhaseLabel.Text = phaseLabel;
        CompactPhase.Text = phaseLabel;
        PomoText.Text = pomo;
        CompactPomo.Text = pomo;
        Prog.Value = Math.Clamp(progress, 0, 1);
        var pauseGlyph = isRunning ? ((char)0xE769).ToString() : ((char)0xE768).ToString();   // pausa / play
        PauseIcon.Glyph = pauseGlyph;
        CompactPauseIcon.Glyph = pauseGlyph;
        SkipBtn.IsEnabled = canSkip;
    }

    private void PauseBtn_Click(object sender, RoutedEventArgs e) => PauseResumeRequested?.Invoke();
    private void SkipBtn_Click(object sender, RoutedEventArgs e) => SkipRequested?.Invoke();
    private void ExpandBtn_Click(object sender, RoutedEventArgs e) => ExpandRequested?.Invoke();
    private void CompactToggleBtn_Click(object sender, RoutedEventArgs e) => SetCompact(true, save: true);
    private void GrowBtn_Click(object sender, RoutedEventArgs e) => SetCompact(false, save: true);
}
