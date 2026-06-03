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
        _target = null;
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
        _target = target;
        SetCard(badge, title, body, requiresAction, optional);
        NextBtn.Content = Loc.Pick("Siguiente", "Next");
        Visibility = Visibility.Visible;
        Reposition();
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        _target = null;
    }

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
            Place(BandTop, 0, 0, w, h);
            Place(BandBottom, 0, 0, 0, 0);
            Place(BandLeft, 0, 0, 0, 0);
            Place(BandRight, 0, 0, 0, 0);
            Ring.Visibility = Visibility.Collapsed;
            Card.VerticalAlignment = VerticalAlignment.Center;
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

        // 4 bandas alrededor del hueco.
        Place(BandTop, 0, 0, w, hy);
        Place(BandBottom, 0, hy + hh, w, h - (hy + hh));
        Place(BandLeft, 0, hy, hx, hh);
        Place(BandRight, hx + hw, hy, w - (hx + hw), hh);

        // Anillo resaltando el hueco.
        Ring.Visibility = Visibility.Visible;
        Canvas.SetLeft(Ring, hx);
        Canvas.SetTop(Ring, hy);
        Ring.Width = hw;
        Ring.Height = hh;

        // Tarjeta en la mitad opuesta al hueco para no taparlo.
        Card.VerticalAlignment = (hy + hh / 2 < h / 2) ? VerticalAlignment.Bottom : VerticalAlignment.Top;
    }

    private static void Place(Rectangle r, double left, double top, double width, double height)
    {
        Canvas.SetLeft(r, left);
        Canvas.SetTop(r, top);
        r.Width = Math.Max(0, width);
        r.Height = Math.Max(0, height);
    }

    private void NextBtn_Click(object sender, RoutedEventArgs e) => Next?.Invoke(this, EventArgs.Empty);
    private void SkipStepBtn_Click(object sender, RoutedEventArgs e) => SkipStep?.Invoke(this, EventArgs.Empty);
    private void SkipAllBtn_Click(object sender, RoutedEventArgs e) => SkipAll?.Invoke(this, EventArgs.Empty);
}
