using Ritmo.Core.Pomodoro;

namespace Ritmo.Core.Tests;

public class PomodoroConfigByNameTests
{
    [Theory]
    [InlineData("Classic")]
    [InlineData("classic")]
    [InlineData("  Clásico ")]
    public void ByName_resuelve_Classic(string name)
        => Assert.Equal(PomodoroConfig.Classic, PomodoroConfig.ByName(name));

    [Theory]
    [InlineData("DeepWork")]
    [InlineData("deep work")]
    [InlineData("deep")]
    public void ByName_resuelve_DeepWork(string name)
        => Assert.Equal(PomodoroConfig.DeepWork, PomodoroConfig.ByName(name));

    [Fact]
    public void ByName_desconocido_usa_fallback()
    {
        var fb = PomodoroConfig.Classic;
        Assert.Equal(fb, PomodoroConfig.ByName("noexiste", fb));
    }

    [Fact]
    public void ByName_null_sin_fallback_da_DeepWork()
        => Assert.Equal(PomodoroConfig.DeepWork, PomodoroConfig.ByName(null));
}
