using System;
using Microsoft.UI.Xaml.Controls;

namespace Ritmo_App.Services;

/// <summary>
/// Mantiene coherente una pareja de <see cref="TimePicker"/> inicio/fin: la hora de fin nunca
/// puede ser anterior NI igual a la de inicio. Si el usuario mueve el inicio por delante del fin,
/// arrastra el fin conservando la duración previa; si pone un fin ≤ inicio, lo ajusta al inicio
/// más un incremento mínimo. Así no hace falta "vigilar" el cruce de medianoche en cada formulario.
/// </summary>
public static class TimeRangeGuard
{
    public static void Attach(TimePicker start, TimePicker end, int minGapMinutes = 5)
    {
        bool syncing = false;                                   // evita la reentrada al ajustar el otro picker
        var minGap = TimeSpan.FromMinutes(minGapMinutes);
        var dayMax = TimeSpan.FromHours(24) - minGap;           // tope para no salirse del día

        start.TimeChanged += (_, e) =>
        {
            if (syncing || end.Time > start.Time) return;       // ya es válido: no toques nada
            syncing = true;
            var priorDur = end.Time - e.OldTime;                // duración antes de mover el inicio
            if (priorDur < minGap) priorDur = minGap;
            var newEnd = start.Time + priorDur;
            if (newEnd > dayMax) newEnd = dayMax;
            end.Time = newEnd;
            syncing = false;
        };

        end.TimeChanged += (_, _) =>
        {
            if (syncing || end.Time > start.Time) return;       // ya es válido
            syncing = true;
            var snapped = start.Time + minGap;                  // mínimo válido: inicio + un incremento
            if (snapped > dayMax) snapped = dayMax;
            end.Time = snapped;
            syncing = false;
        };
    }
}
