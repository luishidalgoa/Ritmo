using Ritmo.Core.Commands;

namespace Ritmo.Core.Tests;

/// <summary>Vista previa del día al iniciar concentración (#47): comando + round-trip.</summary>
public class DayPreviewConfigTests
{
    private static ConfigurationService New() => new(new InMemorySettingsStore());

    [Fact]
    public void Por_defecto_esta_activada()
        => Assert.True(New().GetSettings().ViewConfig.ShowDayPreviewOnFocusStart);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetShowDayPreview_persiste_y_sobrevive_round_trip(bool show)
    {
        var svc = New();
        var r = svc.SetShowDayPreviewOnFocusStart(show);
        Assert.True(r.Success);
        Assert.Equal(show, svc.GetSettings().ViewConfig.ShowDayPreviewOnFocusStart);

        var svc2 = New();
        svc2.ImportJson(svc.ExportJson());
        Assert.Equal(show, svc2.GetSettings().ViewConfig.ShowDayPreviewOnFocusStart);
    }
}
