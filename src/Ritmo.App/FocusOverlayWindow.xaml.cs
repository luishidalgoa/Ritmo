using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Ritmo_App;

/// <summary>
/// Isla flotante de concentración (#118): ventana pequeña, siempre visible, en la
/// esquina superior derecha, con estética "modo reposo (StandBy)" de iPhone — fondo
/// negro y hora gigante de color. Muestra la hora del sistema (protagonista) + el
/// cronómetro Pomodoro y permite pausar/reanudar, saltar y abrir la app.
///
/// No posee el motor: la actualiza y la controla <see cref="TimerPage"/> (mismo
/// hilo de UI) vía <see cref="UpdateView"/> y los eventos de abajo.
/// </summary>
public sealed partial class FocusOverlayWindow : Window
{
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);

    public event Action? ExpandRequested;
    public event Action? PauseResumeRequested;
    public event Action? SkipRequested;

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
        PositionTopRight();
    }

    /// <summary>Dimensiona y coloca la isla en la esquina superior derecha del escritorio.</summary>
    private void PositionTopRight()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double scale = GetDpiForWindow(hwnd) / 96.0;
        if (scale <= 0) scale = 1.0;

        int w = (int)(452 * scale);
        int h = (int)(208 * scale);
        AppWindow.Resize(new SizeInt32(w, h));

        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var wa = area.WorkArea;
        int margin = (int)(18 * scale);
        AppWindow.Move(new PointInt32(wa.X + wa.Width - w - margin, wa.Y + margin));
    }

    /// <summary>Refresca los textos/progreso (lo llama TimerPage en cada tick).</summary>
    public void UpdateView(string clock, string date, string phaseLabel, string pomo,
                           double progress, bool isRunning, bool canSkip)
    {
        ClockText.Text = clock;
        DateText.Text = date;
        PhaseLabel.Text = phaseLabel;
        PomoText.Text = pomo;
        Prog.Value = Math.Clamp(progress, 0, 1);
        PauseIcon.Glyph = isRunning ? "" : "";   // pausa / play
        SkipBtn.IsEnabled = canSkip;
    }

    private void PauseBtn_Click(object sender, RoutedEventArgs e) => PauseResumeRequested?.Invoke();
    private void SkipBtn_Click(object sender, RoutedEventArgs e) => SkipRequested?.Invoke();
    private void ExpandBtn_Click(object sender, RoutedEventArgs e) => ExpandRequested?.Invoke();
}
