using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Ritmo_App.Services;
using Windows.Foundation;

namespace Ritmo_App.Controls;

/// <summary>
/// Capa "coach mark" del tutorial (#tutorial): oscurece la ventana y abre un recorte sobre
/// el control objetivo, con una tarjeta de instrucción. No decide el guion; solo pinta y
/// emite eventos (Next / SkipStep / SkipAll). El controlador (MainWindow) secuencia los pasos.
/// </summary>
public sealed partial class TutorialOverlay : UserControl
{
    private FrameworkElement? _target;

    /// <summary>El usuario pulsó "Siguiente" en un paso informativo.</summary>
    public event EventHandler? Next;
    /// <summary>El usuario decidió saltarse un paso OPCIONAL.</summary>
    public event EventHandler? SkipStep;
    /// <summary>El usuario quiere abandonar TODO el tutorial.</summary>
    public event EventHandler? SkipAll;

    // Último estado aplicado (guard anti-bucle de layout): evita re-pintar cuando el recorte no ha
    // cambiado (y con ello cortar el bucle de re-layout).
    private bool _applied;
    private bool _appliedFull;
    private double _ax, _ay, _aw, _ah;

    // Re-posicionado durante ~700ms tras abrir un spotlight: cubre la ANIMACIÓN del panel lateral
    // (el botón objetivo cambia de POSICIÓN, no de tamaño → SizeChanged no basta) y la medición tardía.
    private DispatcherTimer? _settle;
    private int _settleTicks;

    // ¿El oscurecido COMPLETO (sin hueco) traga los clics? En pasos informativos sí (Siguiente en la
    // tarjeta); en pasos gated por acción (p. ej. "crea una fase") NO, para que el usuario pueda pulsar
    // el botón real. Alrededor de un hueco (spotlight) siempre se bloquea.
    private bool _blockInput = true;

    public TutorialOverlay()
    {
        InitializeComponent();
        SkipStepBtn.Content = Loc.Pick("Saltar paso", "Skip step");
        SkipAllBtn.Content = Loc.Pick("Saltar tutorial", "Skip tutorial");
        SizeChanged += (_, _) => Reposition();
    }

    /// <summary>
    /// Tarjeta centrada SIN recorte (bienvenida, cierre, pasos informativos). Si
    /// <paramref name="requiresAction"/> es true, oculta "Siguiente": el avance lo decide una
    /// acción externa que el controlador detecta (paso con gate de estado, p. ej. "crea una fase").
    /// </summary>
    public void Message(string badge, string title, string body, bool optional = false,
                        string? nextLabel = null, bool requiresAction = false)
    {
        SetTarget(null);
        _applied = false;
        _blockInput = !requiresAction;   // gated (sin Siguiente) → no bloquear: deja pulsar el botón real
        SetCard(badge, title, body, requiresAction, optional);
        NextBtn.Content = nextLabel ?? Loc.Pick("Siguiente", "Next");
        Visibility = Visibility.Visible;
        Reposition();
    }

    /// <summary>
    /// Recorte (spotlight) sobre <paramref name="target"/>. Si <paramref name="requiresAction"/>
    /// es true, NO hay botón "Siguiente": el usuario debe pulsar el control real por el hueco y
    /// el controlador avanza al detectar la acción. Si es false, la tarjeta trae "Siguiente".
    /// </summary>
    public void Spotlight(FrameworkElement target, string badge, string title, string body,
                          bool requiresAction, bool optional = false)
    {
        SetTarget(target);
        _applied = false;
        _blockInput = true;   // spotlight: bloquea todo salvo el hueco
        SetCard(badge, title, body, requiresAction, optional);
        NextBtn.Content = Loc.Pick("Siguiente", "Next");
        Visibility = Visibility.Visible;
        Reposition();
        StartSettle();   // recoloca durante la animación de apertura / medición tardía del objetivo
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        _settle?.Stop();
        SetTarget(null);
    }

    /// <summary>Reposiciona repetidamente ~700ms (cada 50ms) tras abrir un spotlight, luego para.</summary>
    private void StartSettle()
    {
        _settleTicks = 0;
        _settle ??= CreateSettleTimer();
        _settle.Start();
    }

    private DispatcherTimer CreateSettleTimer()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        t.Tick += (_, _) =>
        {
            Reposition();
            if (++_settleTicks >= 14) t.Stop();
        };
        return t;
    }

    /// <summary>
    /// Cambia el objetivo y se suscribe a SU SizeChanged (puntual): cuando el control recién creado se
    /// MIDE (0 → tamaño real), recalcula el recorte UNA vez. Se evita LayoutUpdated global (se disparaba
    /// durante el baile de layout de un ComboBox y, con valores transitorios NaN, crasheaba XAML #crash).
    /// </summary>
    private void SetTarget(FrameworkElement? t)
    {
        if (ReferenceEquals(_target, t)) return;
        if (_target is not null) _target.SizeChanged -= OnTargetSizeChanged;
        _target = t;
        if (_target is not null) _target.SizeChanged += OnTargetSizeChanged;
    }

    private void OnTargetSizeChanged(object sender, SizeChangedEventArgs e) => Reposition();

    private void SetCard(string badge, string title, string body, bool requiresAction, bool optional)
    {
        StepBadge.Text = badge;
        CardTitle.Text = title;
        CardBody.Text = body;
        NextBtn.Visibility = requiresAction ? Visibility.Collapsed : Visibility.Visible;
        SkipStepBtn.Visibility = optional ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Recalcula bandas, anillo y lado de la tarjeta. Idempotente; llamar tras navegar/redimensionar.</summary>
    public void Reposition()
    {
        if (Visibility != Visibility.Visible) return;

        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Sin objetivo (o aún sin medir): oscurecido completo, tarjeta centrada.
        if (_target is null || _target.ActualWidth <= 0 || _target.ActualHeight <= 0)
        {
            ApplyFull(w, h);
            return;
        }

        // Rect del objetivo relativo a esta capa (mismo origen que el Canvas).
        Point p;
        try
        {
            p = _target.TransformToVisual(this).TransformPoint(new Point(0, 0));
        }
        catch
        {
            return; // el objetivo aún no está en el árbol visual
        }

        const double pad = 8;
        double hx = p.X - pad, hy = p.Y - pad;
        double hw = _target.ActualWidth + 2 * pad, hh = _target.ActualHeight + 2 * pad;
        if (hx < 0) { hw += hx; hx = 0; }
        if (hy < 0) { hh += hy; hy = 0; }
        if (hx + hw > w) hw = w - hx;
        if (hy + hh > h) hh = h - hy;

        // Blindaje: en estados de layout transitorios (p. ej. abrir un ComboBox) TransformToVisual puede
        // devolver NaN/∞; asignar eso a XAML lanza E_INVALIDARG y CRASHEA. Si no es finito, no tocar nada.
        if (!double.IsFinite(hx) || !double.IsFinite(hy) || !double.IsFinite(hw) || !double.IsFinite(hh))
            return;

        ApplyHole(hx, hy, hw, hh, w, h);
    }

    /// <summary>Oscurecido completo (sin hueco). Solo repinta si cambió (evita bucle de LayoutUpdated).</summary>
    private void ApplyFull(double w, double h)
    {
        if (_applied && _appliedFull && Same(_aw, w) && Same(_ah, h)) return;
        _applied = true; _appliedFull = true; _aw = w; _ah = h;
        Place(BandTop, 0, 0, w, h);
        Place(BandBottom, 0, 0, 0, 0);
        Place(BandLeft, 0, 0, 0, 0);
        Place(BandRight, 0, 0, 0, 0);
        SetBandsHitTest(_blockInput);   // pasos gated: dim decorativo (no traga clics) → deja pulsar el botón real
        Ring.Visibility = Visibility.Collapsed;
        Card.VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>4 bandas + anillo alrededor del hueco. Solo repinta si cambió (evita bucle de LayoutUpdated).</summary>
    private void ApplyHole(double hx, double hy, double hw, double hh, double w, double h)
    {
        if (_applied && !_appliedFull && Same(_ax, hx) && Same(_ay, hy) && Same(_aw, hw) && Same(_ah, hh)) return;
        _applied = true; _appliedFull = false; _ax = hx; _ay = hy; _aw = hw; _ah = hh;

        Place(BandTop, 0, 0, w, hy);
        Place(BandBottom, 0, hy + hh, w, h - (hy + hh));
        Place(BandLeft, 0, hy, hx, hh);
        Place(BandRight, hx + hw, hy, w - (hx + hw), hh);
        SetBandsHitTest(true);   // alrededor del hueco siempre se bloquea (forzar el control resaltado)

        Ring.Visibility = Visibility.Visible;
        Canvas.SetLeft(Ring, hx);
        Canvas.SetTop(Ring, hy);
        Ring.Width = Math.Max(0, hw);
        Ring.Height = Math.Max(0, hh);

        // Tarjeta en la mitad opuesta al hueco para no taparlo.
        Card.VerticalAlignment = (hy + hh / 2 < h / 2) ? VerticalAlignment.Bottom : VerticalAlignment.Top;
    }

    private static bool Same(double a, double b) => System.Math.Abs(a - b) < 0.5;

    private static void Place(Rectangle r, double left, double top, double width, double height)
    {
        // Nunca asignar NaN/∞ a XAML (E_INVALIDARG → crash). Si algún valor no es finito, no tocar.
        if (!double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(width) || !double.IsFinite(height))
            return;
        Canvas.SetLeft(r, left);
        Canvas.SetTop(r, top);
        r.Width = Math.Max(0, width);
        r.Height = Math.Max(0, height);
    }

    /// <summary>¿Las bandas oscuras tragan los clics? (false = dim decorativo que deja pasar el clic).</summary>
    private void SetBandsHitTest(bool v)
    {
        BandTop.IsHitTestVisible = v;
        BandBottom.IsHitTestVisible = v;
        BandLeft.IsHitTestVisible = v;
        BandRight.IsHitTestVisible = v;
    }

    private void NextBtn_Click(object sender, RoutedEventArgs e) => Next?.Invoke(this, EventArgs.Empty);
    private void SkipStepBtn_Click(object sender, RoutedEventArgs e) => SkipStep?.Invoke(this, EventArgs.Empty);
    private void SkipAllBtn_Click(object sender, RoutedEventArgs e) => SkipAll?.Invoke(this, EventArgs.Empty);
}
