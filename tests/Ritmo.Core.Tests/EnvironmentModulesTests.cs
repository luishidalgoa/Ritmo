using System.Linq;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

/// <summary>Módulos de un entorno como contexto de trabajo (#76): derivación pura + resúmenes.</summary>
public class EnvironmentModulesTests
{
    private static FocusEnvironment Empty() => new() { Id = "e1", Name = "Vacío" };

    [Fact]
    public void For_devuelve_los_cuatro_modulos_en_orden()
    {
        var mods = EnvironmentModules.For(Empty());
        Assert.Equal(
            [EnvironmentModuleKind.Focus, EnvironmentModuleKind.Links, EnvironmentModuleKind.Tasks, EnvironmentModuleKind.Tools],
            mods.Select(m => m.Kind).ToArray());
    }

    [Fact]
    public void Los_cuatro_modulos_son_accionables()
    {
        var mods = EnvironmentModules.For(Empty()).ToDictionary(m => m.Kind);
        Assert.True(mods[EnvironmentModuleKind.Focus].Available);
        Assert.True(mods[EnvironmentModuleKind.Links].Available);
        Assert.True(mods[EnvironmentModuleKind.Tools].Available);   // #78: abrir workspace
        Assert.True(mods[EnvironmentModuleKind.Tasks].Available);   // #125: editor de tareas
    }

    [Fact]
    public void TasksSummary_cuenta_pendientes()
    {
        Assert.Equal("Sin tareas", EnvironmentModules.TasksSummary(Empty()));

        var env = new FocusEnvironment
        {
            Id = "x", Name = "x",
            Tasks =
            [
                new EnvironmentTask { Id = "1", Text = "a", Done = true },
                new EnvironmentTask { Id = "2", Text = "b", Done = false },
            ]
        };
        Assert.Equal("1/2 pendientes", EnvironmentModules.TasksSummary(env));

        var allDone = env with { Tasks = env.Tasks.Select(t => t with { Done = true }).ToList() };
        Assert.Equal("Todas hechas (2)", EnvironmentModules.TasksSummary(allDone));
    }

    [Fact]
    public void FocusSummary_vacio_dice_sin_acciones()
        => Assert.Equal("Sin acciones extra", EnvironmentModules.FocusSummary(
            new FocusEnvironment { Id = "x", Name = "x", EnableDoNotDisturb = false }));

    [Fact]
    public void FocusSummary_compone_las_acciones_activas()
    {
        var env = new FocusEnvironment
        {
            Id = "x", Name = "x",
            EnableDoNotDisturb = true,
            AppsToOpen = ["code"],
            AppsToClose = ["Discord", "Steam"],
            BlockedWebsites = ["youtube.com"]
        };
        var s = EnvironmentModules.FocusSummary(env);
        Assert.Contains("No molestar", s);
        Assert.Contains("abre 1 app(s)", s);
        Assert.Contains("cierra 2", s);
        Assert.Contains("bloquea 1 web(s)", s);
    }

    [Theory]
    [InlineData(0, "Sin enlaces")]
    [InlineData(1, "1 enlace")]
    [InlineData(3, "3 enlaces")]
    public void LinksSummary_pluraliza(int count, string expected)
    {
        var links = Enumerable.Range(0, count)
            .Select(i => new ShortcutLink { Title = $"L{i}", Url = $"https://x/{i}" })
            .ToList();
        var env = new FocusEnvironment { Id = "x", Name = "x", Links = links };
        Assert.Equal(expected, EnvironmentModules.LinksSummary(env));
    }
}
