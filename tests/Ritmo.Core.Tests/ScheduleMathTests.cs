using System;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

public class ScheduleMathTests
{
    [Fact]
    public void Duracion_normal()
        => Assert.Equal(TimeSpan.FromHours(2),
            ScheduleMath.DurationBetween(new TimeOnly(9, 0), new TimeOnly(11, 0)));

    [Fact]
    public void Duracion_con_minutos()
        => Assert.Equal(TimeSpan.FromMinutes(90),
            ScheduleMath.DurationBetween(new TimeOnly(16, 40), new TimeOnly(18, 10)));

    [Fact]
    public void Cruza_medianoche()
        => Assert.Equal(TimeSpan.FromHours(2),
            ScheduleMath.DurationBetween(new TimeOnly(23, 0), new TimeOnly(1, 0)));

    [Fact]
    public void Iguales_da_cero()
        => Assert.Equal(TimeSpan.Zero,
            ScheduleMath.DurationBetween(new TimeOnly(9, 0), new TimeOnly(9, 0)));

    [Theory]
    [InlineData(9, 0, 2, 10, 0)]    // +2 slots de 30' = +1h
    [InlineData(9, 0, -1, 8, 30)]   // -1 slot = -30'
    [InlineData(9, 30, 3, 11, 0)]   // +1h30
    public void ShiftStart_desplaza_por_slots(int h, int m, int delta, int eh, int em)
        => Assert.Equal(new TimeOnly(eh, em), ScheduleMath.ShiftStart(new TimeOnly(h, m), delta));

    [Fact]
    public void ShiftStart_no_baja_de_medianoche()
        => Assert.Equal(new TimeOnly(0, 0), ScheduleMath.ShiftStart(new TimeOnly(0, 30), -5));

    [Fact]
    public void ShiftStart_no_pasa_del_ultimo_slot_del_dia()
        => Assert.Equal(new TimeOnly(23, 30), ScheduleMath.ShiftStart(new TimeOnly(23, 0), 10));

    [Fact]
    public void TimesOverlap_solapan()
        => Assert.True(ScheduleMath.TimesOverlap(
            new TimeOnly(9, 0), TimeSpan.FromHours(2), new TimeOnly(10, 0), TimeSpan.FromHours(1)));

    [Fact]
    public void TimesOverlap_disjuntos_no()
        => Assert.False(ScheduleMath.TimesOverlap(
            new TimeOnly(9, 0), TimeSpan.FromHours(1), new TimeOnly(11, 0), TimeSpan.FromHours(1)));

    [Fact]
    public void TimesOverlap_bordes_que_se_tocan_no_solapan()
        => Assert.False(ScheduleMath.TimesOverlap(
            new TimeOnly(9, 0), TimeSpan.FromHours(2), new TimeOnly(11, 0), TimeSpan.FromHours(1)));

    [Fact]
    public void TimesOverlap_uno_dentro_de_otro()
        => Assert.True(ScheduleMath.TimesOverlap(
            new TimeOnly(9, 0), TimeSpan.FromHours(3), new TimeOnly(10, 0), TimeSpan.FromMinutes(30)));
}
