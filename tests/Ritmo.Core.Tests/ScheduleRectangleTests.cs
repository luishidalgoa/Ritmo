using System;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

public class ScheduleRectangleTests
{
    private static TimeOnly T(int h, int m = 0) => new(h, m);

    [Fact]
    public void Dentro_de_columnas_y_franja()
        => Assert.True(ScheduleRectangle.InRectangle(2, T(10), T(11), 1, 3, T(9), T(12)));

    [Fact]
    public void Fuera_por_columna_izquierda()
        => Assert.False(ScheduleRectangle.InRectangle(0, T(10), T(11), 1, 3, T(9), T(12)));

    [Fact]
    public void Fuera_por_columna_derecha()
        => Assert.False(ScheduleRectangle.InRectangle(4, T(10), T(11), 1, 3, T(9), T(12)));

    [Fact]
    public void Fuera_por_franja_antes()
        => Assert.False(ScheduleRectangle.InRectangle(2, T(7), T(8), 1, 3, T(9), T(12)));

    [Fact]
    public void Fuera_por_franja_despues()
        => Assert.False(ScheduleRectangle.InRectangle(2, T(13), T(14), 1, 3, T(9), T(12)));

    [Fact]
    public void Cuenta_si_solapa_parcialmente_la_franja()
    {
        // Empieza antes del rango pero termina dentro -> solapa -> cuenta.
        Assert.True(ScheduleRectangle.InRectangle(2, T(8), T(10), 1, 3, T(9), T(12)));
        // Empieza dentro y termina después -> solapa -> cuenta.
        Assert.True(ScheduleRectangle.InRectangle(2, T(11), T(13), 1, 3, T(9), T(12)));
    }

    [Fact]
    public void Bordes_de_columna_inclusivos()
    {
        Assert.True(ScheduleRectangle.InRectangle(1, T(10), T(11), 1, 3, T(9), T(12)));
        Assert.True(ScheduleRectangle.InRectangle(3, T(10), T(11), 1, 3, T(9), T(12)));
    }

    [Fact]
    public void Una_sola_celda()
        => Assert.True(ScheduleRectangle.InRectangle(2, T(10), T(11), 2, 2, T(10), T(11)));
}
