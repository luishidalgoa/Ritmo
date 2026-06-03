using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Ritmo_App.Controls;

namespace Ritmo_App.Services;

/// <summary>Cómo terminó la espera de un paso del tutorial.</summary>
public enum StepOutcome
{
    /// <summary>El usuario pulsó "Siguiente" (paso informativo).</summary>
    Next,
    /// <summary>El usuario realizó la acción real exigida (clic en el control objetivo, guardar…).</summary>
    ActionDone,
    /// <summary>El usuario se saltó un paso OPCIONAL.</summary>
    SkipStep,
    /// <summary>El usuario abandonó todo el tutorial (tras confirmar).</summary>
    SkipAll
}

/// <summary>
/// Motor del tutorial "coach mark" (#tutorial). Maneja la capa <see cref="TutorialOverlay"/> y
/// secuencia los pasos: cada paso muestra una tarjeta y espera a que el usuario (a) pulse
/// Siguiente, (b) realice la acción real exigida —que el guion señala con <see cref="SignalAction"/>—,
/// (c) se salte un paso opcional o (d) abandone el tutorial (con confirmación). El GUION concreto
/// (los 15 pasos) vive en MainWindow, que conoce los controles reales.
/// </summary>
public sealed class TutorialController
{
    private readonly TutorialOverlay _overlay;
    private TaskCompletionSource<StepOutcome>? _current;

    /// <summary>True si el usuario abandonó el tutorial a mitad (no completar → no persistir).</summary>
    public bool Aborted { get; private set; }

    public TutorialController(TutorialOverlay overlay)
    {
        _overlay = overlay;
        _overlay.Next += (_, _) => _current?.TrySetResult(StepOutcome.Next);
        _overlay.SkipStep += (_, _) => _current?.TrySetResult(StepOutcome.SkipStep);
        _overlay.SkipAll += (_, _) => _current?.TrySetResult(StepOutcome.SkipAll);
    }

    /// <summary>El guion llama a esto cuando detecta que el usuario hizo la acción real del paso.</summary>
    public void SignalAction() => _current?.TrySetResult(StepOutcome.ActionDone);

    /// <summary>Tarjeta centrada SIN recorte (bienvenida, cierre, explicaciones). Avanza con "Siguiente".</summary>
    public Task<bool> Message(string badge, string title, string body, bool optional = false, string? nextLabel = null)
    {
        _overlay.Message(badge, title, body, optional, nextLabel);
        return Await(null, null);
    }

    /// <summary>
    /// Recorte sobre <paramref name="target"/> que se completa cuando el usuario PULSA ese control
    /// (evento Tapped genérico). Para nav items y botones simples.
    /// </summary>
    public Task<bool> SpotlightClick(FrameworkElement target, string badge, string title, string body,
                                     bool optional = false)
    {
        void OnTapped(object s, TappedRoutedEventArgs e) => SignalAction();
        _overlay.Spotlight(target, badge, title, body, requiresAction: true, optional);
        return Await(() => target.Tapped += OnTapped, () => target.Tapped -= OnTapped);
    }

    /// <summary>
    /// Recorte sobre <paramref name="target"/> con detección de avance A MEDIDA: el guion suscribe en
    /// <paramref name="subscribe"/> el/los eventos que indican que la acción ocurrió (cada handler debe
    /// llamar a <see cref="SignalAction"/>) y los retira en <paramref name="unsubscribe"/>. Útil cuando
    /// "hacer la acción" no es un clic en el propio objetivo (p. ej. guardar un diálogo, abrir un panel).
    /// </summary>
    public Task<bool> SpotlightUntil(FrameworkElement target, string badge, string title, string body,
                                     Action subscribe, Action unsubscribe, bool optional = false)
    {
        _overlay.Spotlight(target, badge, title, body, requiresAction: true, optional);
        return Await(subscribe, unsubscribe);
    }

    /// <summary>
    /// Tarjeta informativa (centrada) cuyo avance lo decide una acción externa (no el botón Siguiente):
    /// el guion suscribe sus eventos en <paramref name="subscribe"/>. Para pasos sin un control concreto
    /// que recortar (p. ej. "se ha abierto la isla", "se ha guardado la nota").
    /// </summary>
    public Task<bool> MessageUntil(string badge, string title, string body,
                                   Action subscribe, Action unsubscribe, bool optional = false)
    {
        _overlay.Message(badge, title, body, optional, nextLabel: null, requiresAction: true);
        return Await(subscribe, unsubscribe);
    }

    /// <summary>Reposiciona el recorte tras un cambio de layout/navegación.</summary>
    public void Reposition() => _overlay.Reposition();

    /// <summary>Oculta la capa al terminar.</summary>
    public void Finish() => _overlay.Hide();

    /// <summary>
    /// Espera el desenlace del paso. <paramref name="subscribe"/>/<paramref name="unsubscribe"/> conectan
    /// la detección de la acción real (si la hay). Devuelve true para AVANZAR (Siguiente / acción / saltar
    /// paso) y false si el usuario abandonó el tutorial.
    /// </summary>
    private async Task<bool> Await(Action? subscribe, Action? unsubscribe)
    {
        subscribe?.Invoke();
        try
        {
            while (true)
            {
                _current = new TaskCompletionSource<StepOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
                var outcome = await _current.Task;

                if (outcome != StepOutcome.SkipAll)
                    return true;   // Next / ActionDone / SkipStep → avanzar

                if (await ConfirmAbort())
                {
                    Aborted = true;
                    return false;  // abandona el tutorial
                }
                // declinó abandonar → vuelve a esperar el mismo paso (la tarjeta sigue visible)
            }
        }
        finally
        {
            unsubscribe?.Invoke();
        }
    }

    private async Task<bool> ConfirmAbort()
    {
        var dlg = new ContentDialog
        {
            XamlRoot = _overlay.XamlRoot,
            Title = Loc.Pick("¿Salir del tutorial?", "Exit the tutorial?"),
            Content = Loc.Pick(
                "Podrás usar Ritmo por tu cuenta. No se guardará el horario de ejemplo.",
                "You can use Ritmo on your own. The example schedule won't be saved."),
            PrimaryButtonText = Loc.Pick("Salir", "Exit"),
            CloseButtonText = Loc.Pick("Seguir con el tutorial", "Keep going"),
            DefaultButton = ContentDialogButton.Close
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }
}
