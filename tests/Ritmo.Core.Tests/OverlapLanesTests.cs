using System;
using System.Linq;
using Ritmo.Core.Model;
using Ritmo.Core.Scheduling;

namespace Ritmo.Core.Tests;

/// <summary>Reparto en carriles de sesiones solapadas el mismo día (#130).</summary>
public class OverlapLanesTests
{
    private static StudySession S(int h, int m, double hours) => new()
    {
        Title = $"{h:00}:{m:00}", Day = DayOfWeek.Monday,
        Start = new TimeOnly(h, m), Duration = TimeSpan.FromHours(hours), CategoryId = "Otro"
    };

    private static (int lane, int count) For(System.Collections.Generic.IReadOnlyList<LaneAssignment> a, string title)
    {
        var x = a.Single(z => z.Session.Title == title);
        return (x.Lane, x.LaneCount);
    }

    [Fact]
    public void Sin_solape_todas_en_carril_0_con_count_1()
    {
        var a = OverlapLanes.Assign([S(9, 0, 1), S(10, 0, 1), S(11, 0, 1)]);
        Assert.All(a, x => Assert.Equal(0, x.Lane));
        Assert.All(a, x => Assert.Equal(1, x.LaneCount));
    }

    [Fact]
    public void Dos_que_se_solapan_van_a_carriles_0_y_1_con_count_2()
    {
        var a = OverlapLanes.Assign([S(9, 0, 2), S(10, 0, 1)]);   // 9-11 y 10-11 se solapan
        Assert.Equal(2, For(a, "09:00").count);
        Assert.Equal(2, For(a, "10:00").count);
        Assert.NotEqual(For(a, "09:00").lane, For(a, "10:00").lane);
    }

    [Fact]
    public void Tres_simultaneas_usan_tres_carriles()
    {
        var a = OverlapLanes.Assign([S(9, 0, 2), S(9, 30, 2), S(10, 0, 2)]);   // todas solapan
        Assert.All(a, x => Assert.Equal(3, x.LaneCount));
        Assert.Equal(new[] { 0, 1, 2 }, a.OrderBy(x => x.Session.Start).Select(x => x.Lane).ToArray());
    }

    [Fact]
    public void Un_grupo_solapado_no_afecta_a_otra_sesion_separada()
    {
        var a = OverlapLanes.Assign([S(9, 0, 2), S(10, 0, 1), S(14, 0, 1)]);   // 9-11 + 10-11 solapan; 14-15 aparte
        Assert.Equal(2, For(a, "09:00").count);
        Assert.Equal(2, For(a, "10:00").count);
        Assert.Equal(1, For(a, "14:00").count);   // suelta: carril 0, count 1
        Assert.Equal(0, For(a, "14:00").lane);
    }

    [Fact]
    public void Cadena_de_solape_reusa_carril_libre()
    {
        // A 9-10, B 9:30-10:30, C 10:15-11. A&B solapan, B&C solapan, A&C no.
        var a = OverlapLanes.Assign([S(9, 0, 1), S(9, 30, 1), S(10, 15, 0.75)]);
        // Componente conexa (A-B-C); C reusa el carril de A (libre a las 10) -> 2 carriles.
        Assert.All(a, x => Assert.Equal(2, x.LaneCount));
        Assert.Equal(For(a, "09:00").lane, For(a, "10:15").lane);   // A y C comparten carril
        Assert.NotEqual(For(a, "09:00").lane, For(a, "09:30").lane);
    }

    [Fact]
    public void Lista_vacia_no_falla()
    {
        Assert.Empty(OverlapLanes.Assign([]));
    }
}
