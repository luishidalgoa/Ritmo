using System;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

public class TimeSlotsTests
{
    [Fact]
    public void Sin_minimo_todo_habilitado()
    {
        Assert.True(TimeSlots.HourEnabled(0, 5, null));
        Assert.True(TimeSlots.MinuteEnabled(0, 0, null));
    }

    [Fact]
    public void Horas_anteriores_al_minimo_deshabilitadas()
    {
        var min = new TimeOnly(15, 0);
        Assert.False(TimeSlots.HourEnabled(14, 5, min));   // 14:55 <= 15:00
        Assert.True(TimeSlots.HourEnabled(15, 5, min));    // 15:55 > 15:00 -> hay minutos válidos
        Assert.True(TimeSlots.HourEnabled(16, 5, min));
    }

    [Fact]
    public void En_la_hora_del_minimo_los_minutos_hasta_el_se_deshabilitan()
    {
        var min = new TimeOnly(15, 0);
        Assert.False(TimeSlots.MinuteEnabled(15, 0, min));  // 15:00 no es > 15:00
        Assert.True(TimeSlots.MinuteEnabled(15, 5, min));   // 15:05 sí
    }

    [Fact]
    public void Minimo_no_alineado_al_paso()
    {
        var min = new TimeOnly(15, 7);                      // entre slots de 5
        Assert.False(TimeSlots.MinuteEnabled(15, 5, min));  // 15:05 <= 15:07
        Assert.True(TimeSlots.MinuteEnabled(15, 10, min));  // 15:10 > 15:07
        Assert.Equal(10, TimeSlots.FirstValidMinute(15, 5, min));
    }

    [Fact]
    public void Hora_del_minimo_sin_minutos_validos_se_deshabilita()
    {
        var min = new TimeOnly(15, 55);                     // último slot de la hora
        Assert.False(TimeSlots.HourEnabled(15, 5, min));    // 15:55 no es > 15:55
        Assert.True(TimeSlots.HourEnabled(16, 5, min));
    }

    [Fact]
    public void FirstValidMinute_primer_slot_valido()
    {
        Assert.Equal(0, TimeSlots.FirstValidMinute(16, 5, new TimeOnly(15, 0)));   // hora posterior: 00 vale
        Assert.Equal(5, TimeSlots.FirstValidMinute(15, 5, new TimeOnly(15, 0)));   // misma hora: 00 no, 05 sí
        Assert.Equal(0, TimeSlots.FirstValidMinute(9, 5, null));
    }
}
