using System;
using System.Collections.Generic;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Persistence;
using Ritmo.Core.Pomodoro;

namespace Ritmo.Core.Tests;

public class PomodoroRhythmTests
{
    private static (ConfigurationService, InMemorySettingsStore) New()
    {
        var store = new InMemorySettingsStore();
        return (new ConfigurationService(store), store);
    }

    private static IReadOnlyList<PomodoroRhythm> None => [];

    // ---------- Modelo puro ----------

    [Fact]
    public void ToConfig_mapea_minutos_a_TimeSpan()
    {
        var r = new PomodoroRhythm { Id = "x", Name = "X", FocusMinutes = 40, ShortBreakMinutes = 8, LongBreakMinutes = 25, FocusesPerLongBreak = 3 };
        var c = r.ToConfig();
        Assert.Equal(TimeSpan.FromMinutes(40), c.Focus);
        Assert.Equal(TimeSpan.FromMinutes(8), c.ShortBreak);
        Assert.Equal(TimeSpan.FromMinutes(25), c.LongBreak);
        Assert.Equal(3, c.FocusesPerLongBreak);
    }

    [Fact]
    public void BuiltIns_son_clasico_y_profundo()
    {
        Assert.Equal(2, PomodoroRhythms.BuiltIns.Count);
        Assert.Equal(PomodoroConfig.Classic, PomodoroRhythms.Classic.ToConfig());
        Assert.Equal(PomodoroConfig.DeepWork, PomodoroRhythms.DeepWork.ToConfig());
        Assert.All(PomodoroRhythms.BuiltIns, r => Assert.True(r.IsBuiltIn));
    }

    [Fact]
    public void All_combina_builtins_y_propios()
    {
        var custom = new[] { new PomodoroRhythm { Id = "mine", Name = "Mío" } };
        var all = PomodoroRhythms.All(custom);
        Assert.Equal(3, all.Count);
        Assert.Contains(all, r => r.Id == "mine");
        Assert.Contains(all, r => r.Id == PomodoroRhythms.ClassicId);
    }

    [Fact]
    public void Resolve_id_vacio_usa_el_por_defecto()
    {
        var appDefault = PomodoroConfig.Classic;
        Assert.Equal(appDefault, PomodoroRhythms.Resolve(null, None, appDefault));
        Assert.Equal(appDefault, PomodoroRhythms.Resolve("", None, appDefault));
        Assert.Equal(appDefault, PomodoroRhythms.Resolve("   ", None, appDefault));
    }

    [Theory]
    [InlineData("classic")]
    [InlineData("Classic")]   // nombre heredado
    public void Resolve_clasico(string id)
        => Assert.Equal(PomodoroConfig.Classic, PomodoroRhythms.Resolve(id, None, PomodoroConfig.DeepWork));

    [Theory]
    [InlineData("deepwork")]
    [InlineData("DeepWork")]  // nombre heredado
    public void Resolve_profundo(string id)
        => Assert.Equal(PomodoroConfig.DeepWork, PomodoroRhythms.Resolve(id, None, PomodoroConfig.Classic));

    [Fact]
    public void Resolve_id_propio()
    {
        var custom = new[] { new PomodoroRhythm { Id = "mine", Name = "Mío", FocusMinutes = 33 } };
        var c = PomodoroRhythms.Resolve("mine", custom, PomodoroConfig.Classic);
        Assert.Equal(TimeSpan.FromMinutes(33), c.Focus);
    }

    [Fact]
    public void Resolve_id_desconocido_usa_el_por_defecto()
    {
        var appDefault = PomodoroConfig.DeepWork;
        Assert.Equal(appDefault, PomodoroRhythms.Resolve("noexiste", None, appDefault));
    }

    [Fact]
    public void Find_devuelve_null_o_la_entrada()
    {
        Assert.Null(PomodoroRhythms.Find(null, None));
        Assert.Equal("Clásico", PomodoroRhythms.Find("classic", None)!.Name);
        Assert.Equal("Profundo", PomodoroRhythms.Find("DeepWork", None)!.Name);   // heredado
    }

    // ---------- CRUD vía servicio ----------

    [Fact]
    public void AddRhythm_crea_y_persiste()
    {
        var (svc, store) = New();
        var r = svc.AddRhythm("Sprint", 30, 6, 18, 3);
        Assert.True(r.Success);
        var rhythm = Assert.Single(store.Load().Rhythms);
        Assert.Equal("Sprint", rhythm.Name);
        Assert.Equal(30, rhythm.FocusMinutes);
        Assert.False(rhythm.IsBuiltIn);
    }

    [Fact]
    public void AddRhythm_rechaza_nombre_vacio_y_focos_invalidos()
    {
        var (svc, _) = New();
        Assert.False(svc.AddRhythm("  ", 30, 5, 15, 4).Success);
        Assert.False(svc.AddRhythm("X", 0, 5, 15, 4).Success);
        Assert.False(svc.AddRhythm("X", 30, 5, 15, 0).Success);
    }

    [Fact]
    public void UpdateRhythm_edita_por_id()
    {
        var (svc, store) = New();
        var id = svc.AddRhythm("Sprint", 30, 6, 18, 3).Message;
        Assert.True(svc.UpdateRhythm(id, "Sprint largo", 45, 9, 22, 2).Success);
        var rhythm = Assert.Single(store.Load().Rhythms);
        Assert.Equal("Sprint largo", rhythm.Name);
        Assert.Equal(45, rhythm.FocusMinutes);
    }

    [Fact]
    public void UpdateRhythm_id_inexistente_falla()
    {
        var (svc, _) = New();
        Assert.False(svc.UpdateRhythm("nope", "X", 30, 5, 15, 4).Success);
    }

    [Fact]
    public void RemoveRhythm_borra_por_id()
    {
        var (svc, store) = New();
        var id = svc.AddRhythm("Sprint", 30, 6, 18, 3).Message;
        Assert.True(svc.RemoveRhythm(id).Success);
        Assert.Empty(store.Load().Rhythms);
    }

    [Fact]
    public void Rhythms_sobreviven_round_trip_json()
    {
        var settings = AppSettings.Default with
        {
            Rhythms = [new PomodoroRhythm { Id = "r1", Name = "Mío", FocusMinutes = 42, ShortBreakMinutes = 7, LongBreakMinutes = 21, FocusesPerLongBreak = 3 }]
        };
        var json = SettingsJson.Serialize(settings);
        var back = SettingsJson.Deserialize(json);
        var rhythm = Assert.Single(back.Rhythms);
        Assert.Equal("Mío", rhythm.Name);
        Assert.Equal(42, rhythm.FocusMinutes);
        Assert.Equal(3, rhythm.FocusesPerLongBreak);
    }
}
