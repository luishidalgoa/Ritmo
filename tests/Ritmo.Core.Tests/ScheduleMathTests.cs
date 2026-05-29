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
}
