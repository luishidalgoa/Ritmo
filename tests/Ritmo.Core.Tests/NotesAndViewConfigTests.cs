using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

public class StudyNoteTests
{
    [Fact]
    public void Nota_guarda_titulo_contenido_y_acento()
    {
        var n = new StudyNote
        {
            Id = "n1",
            Title = "¡OJO! Importante",
            Content = "- El **jurídico** es tu batalla\n- Tests desde el día 1",
            AccentColor = "#C0392B",
            Order = 0
        };
        Assert.Equal("¡OJO! Importante", n.Title);
        Assert.Contains("**jurídico**", n.Content);
        Assert.Equal("#C0392B", n.AccentColor);
    }

    [Fact]
    public void Varias_notas_se_ordenan_por_Order()
    {
        var notes = new List<StudyNote>
        {
            new() { Id = "b", Title = "B", Order = 2 },
            new() { Id = "a", Title = "A", Order = 1 },
            new() { Id = "c", Title = "C", Order = 3 }
        };
        var ordered = notes.OrderBy(n => n.Order).Select(n => n.Title).ToArray();
        Assert.Equal(new[] { "A", "B", "C" }, ordered);
    }

    [Fact]
    public void Contenido_y_acento_son_opcionales()
    {
        var n = new StudyNote { Id = "x", Title = "Vacía" };
        Assert.Equal("", n.Content);
        Assert.Null(n.AccentColor);
    }
}

public class ScheduleViewConfigTests
{
    [Fact]
    public void Valores_por_defecto_son_8_a_20()
    {
        var c = new ScheduleViewConfig();
        Assert.Equal(new TimeOnly(8, 0), c.DayStart);
        Assert.Equal(new TimeOnly(20, 0), c.DayEnd);
        Assert.True(c.ShowDayPreviewOnFocusStart);
    }

    [Fact]
    public void RowCount_cuenta_las_horas_del_rango()
    {
        Assert.Equal(12, new ScheduleViewConfig { DayStart = new(8, 0), DayEnd = new(20, 0) }.RowCount);
        Assert.Equal(2, new ScheduleViewConfig { DayStart = new(9, 0), DayEnd = new(11, 0) }.RowCount);
        // Rango con minutos sueltos redondea hacia arriba (06:00–06:30 -> 1 fila).
        Assert.Equal(1, new ScheduleViewConfig { DayStart = new(6, 0), DayEnd = new(6, 30) }.RowCount);
    }

    [Fact]
    public void RowCount_rango_invalido_es_cero()
    {
        Assert.Equal(0, new ScheduleViewConfig { DayStart = new(20, 0), DayEnd = new(8, 0) }.RowCount);
        Assert.Equal(0, new ScheduleViewConfig { DayStart = new(9, 0), DayEnd = new(9, 0) }.RowCount);
    }

    [Fact]
    public void ColorFor_devuelve_el_configurado_o_null()
    {
        var c = new ScheduleViewConfig
        {
            ColorsByKind = new Dictionary<StudyKind, string>
            {
                [StudyKind.Tecnico] = "#E2EFDA",
                [StudyKind.Legislacion] = "#DCE6F1"
            }
        };
        Assert.Equal("#E2EFDA", c.ColorFor(StudyKind.Tecnico));
        Assert.Equal("#DCE6F1", c.ColorFor(StudyKind.Legislacion));
        Assert.Null(c.ColorFor(StudyKind.Ingles)); // no configurado
    }

    [Fact]
    public void Shortcuts_guarda_enlaces_atajo()
    {
        var c = new ScheduleViewConfig
        {
            Shortcuts =
            [
                new ShortcutLink { Title = "Campus ZBrain", Url = "https://campus.zbrain.es" },
                new ShortcutLink { Title = "BOE", Url = "https://www.boe.es" }
            ]
        };
        Assert.Equal(2, c.Shortcuts.Count);
        Assert.Equal("Campus ZBrain", c.Shortcuts[0].Title);
    }

    [Fact]
    public void Flag_vista_previa_se_puede_desactivar()
    {
        var c = new ScheduleViewConfig { ShowDayPreviewOnFocusStart = false };
        Assert.False(c.ShowDayPreviewOnFocusStart);
    }
}
