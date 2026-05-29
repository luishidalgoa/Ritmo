using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Ritmo_App;

/// <summary>
/// Contenedor de una tarjeta de sesión del horario. Border es sellado en WinUI 3,
/// así que usamos un ContentControl (no sellado) que aloja el Border visual y, al
/// poder heredar, permite cambiar el cursor del ratón (ProtectedCursor) según la
/// zona: mover / redimensionar lateral / vertical / esquina (#82/#90).
/// </summary>
public sealed class SessionCard : ContentControl
{
    public SessionCard()
    {
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
    }

    /// <summary>Cambia el cursor a la forma indicada (mover, redimensionar…).</summary>
    public void SetCursor(InputSystemCursorShape shape)
        => ProtectedCursor = InputSystemCursor.Create(shape);
}
