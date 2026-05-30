using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

/// <summary>
/// Geometría de la rejilla (#61): la granularidad solo cambia las líneas-guía; la
/// posición de los bloques es proporcional a sus minutos reales (granularity-agnóstica).
/// </summary>
public class ScheduleGeometryTests
{
    [Theory]
    [InlineData(60, 60)]
    [InlineData(30, 30)]
    [InlineData(15, 15)]
    [InlineData(45, 60)]   // valor no admitido -> cae a 60
    [InlineData(0, 60)]
    [InlineData(-5, 60)]
    [InlineData(7, 60)]
    public void NormalizeGranularity_solo_admite_60_30_15(int input, int expected) =>
        Assert.Equal(expected, ScheduleGeometry.NormalizeGranularity(input));

    [Theory]
    [InlineData(60, 1)]
    [InlineData(30, 2)]
    [InlineData(15, 4)]
    public void SlotsPerHour(int granularity, int expected) =>
        Assert.Equal(expected, ScheduleGeometry.SlotsPerHour(granularity));

    [Fact]
    public void SlotHeight_mantiene_el_alto_de_hora_constante()
    {
        // Con un alto de hora de 52px, a 30 min cada slot mide 26; a 15, 13.
        Assert.Equal(52, ScheduleGeometry.SlotHeight(52, 60));
        Assert.Equal(26, ScheduleGeometry.SlotHeight(52, 30));
        Assert.Equal(13, ScheduleGeometry.SlotHeight(52, 15));
    }

    [Fact]
    public void TopPixels_es_proporcional_al_minuto_real_e_independiente_de_la_granularidad()
    {
        var t = new TimeOnly(16, 40);
        // (16-8)*60 + 40 = 520 min -> 520/60 * 52 ≈ 450.67 px
        var expected = 520.0 / 60.0 * 52.0;
        Assert.Equal(expected, ScheduleGeometry.TopPixels(t, startHour: 8, hourHeightPx: 52), precision: 6);
        // El valor NO debe depender de la granularidad: no hay parámetro de granularidad aquí.
    }

    [Fact]
    public void TopPixels_en_hora_redonda_cae_justo_en_la_linea()
    {
        // 09:00 desde las 08:00 = 1h -> exactamente un alto de hora.
        Assert.Equal(52, ScheduleGeometry.TopPixels(new TimeOnly(9, 0), 8, 52), precision: 6);
        // La hora de inicio cae en 0.
        Assert.Equal(0, ScheduleGeometry.TopPixels(new TimeOnly(8, 0), 8, 52), precision: 6);
    }

    [Theory]
    [InlineData(60, 52)]    // 1h
    [InlineData(90, 78)]    // 1h30
    [InlineData(40, 34.6666667)]
    public void HeightPixels_proporcional_a_la_duracion(int minutes, double expectedPx) =>
        Assert.Equal(expectedPx, ScheduleGeometry.HeightPixels(TimeSpan.FromMinutes(minutes), 52), precision: 5);

    [Fact]
    public void SlotRows_cubre_el_rango_segun_granularidad()
    {
        // 08:00–20:00 = 12h. 60→12 filas, 30→24, 15→48.
        Assert.Equal(12, ScheduleGeometry.SlotRows(8, 20, 60));
        Assert.Equal(24, ScheduleGeometry.SlotRows(8, 20, 30));
        Assert.Equal(48, ScheduleGeometry.SlotRows(8, 20, 15));
    }

    [Fact]
    public void PixelsToSlots_redondea_al_slot_mas_cercano()
    {
        // alto hora 52, granularidad 30 -> slot 26px. 40px ≈ 1.54 slots -> 2.
        Assert.Equal(2, ScheduleGeometry.PixelsToSlots(40, 52, 30));
        // 12px ≈ 0.46 slot -> 0.
        Assert.Equal(0, ScheduleGeometry.PixelsToSlots(12, 52, 30));
        // negativo (arrastre hacia arriba)
        Assert.Equal(-1, ScheduleGeometry.PixelsToSlots(-26, 52, 30));
    }

    [Theory]
    [InlineData(60, 60)]
    [InlineData(30, 30)]
    [InlineData(15, 15)]
    public void SlotMinutes(int granularity, int expected) =>
        Assert.Equal(expected, ScheduleGeometry.SlotMinutes(granularity));
}
