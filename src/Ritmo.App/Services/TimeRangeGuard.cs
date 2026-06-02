using System;
using Microsoft.UI.Xaml.Controls;

namespace Ritmo_App.Services;

/// <summary>
/// Mantiene coherente una pareja de <see cref="TimePicker"/> inicio/fin: la hora de fin nunca
/// puede ser anterior NI igual a la de inicio. En cuanto el usuario elige un valor que rompe esa
/// regla (un fin ≤ inicio, o un inicio que adelanta al fin), el fin se reajusta al inicio + un
/// incremento mínimo. Así no hay que vigilar el cruce de medianoche en cada formulario.
///
/// Nota WinUI 3: el TimePicker dispara <c>SelectedTimeChanged</c> de forma fiable (no siempre
/// <c>TimeChanged</c>), y una vez elegido muestra <c>SelectedTime</c>; por eso se escuchan AMBOS
/// eventos y se corrigen AMBAS propiedades (<c>Time</c> y <c>SelectedTime</c>).
/// </summary>
public static class TimeRangeGuard
{
    public static void Attach(TimePicker start, TimePicker end, int minGapMinutes = 5)
    {
        bool syncing = false;                                   // evita reentrada al reajustar el fin
        var minGap = TimeSpan.FromMinutes(minGapMinutes);
        var dayMax = TimeSpan.FromHours(24) - minGap;           // tope para no salirse del día

        static TimeSpan Cur(TimePicker p) => p.SelectedTime ?? p.Time;

        void Enforce()
        {
            if (syncing || Cur(end) > Cur(start)) return;       // ya es válido: no toques nada
            syncing = true;
            var v = Cur(start) + minGap;                        // mínimo válido: inicio + un incremento
            if (v > dayMax) v = dayMax;
            end.Time = v;
            end.SelectedTime = v;                              // lo que el control MUESTRA tras elegir
            syncing = false;
        }

        start.SelectedTimeChanged += (_, _) => Enforce();
        start.TimeChanged += (_, _) => Enforce();
        end.SelectedTimeChanged += (_, _) => Enforce();
        end.TimeChanged += (_, _) => Enforce();
    }
}
