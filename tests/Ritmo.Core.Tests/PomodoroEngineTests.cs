using Ritmo.Core.Pomodoro;

namespace Ritmo.Core.Tests;

public class PomodoroConfigTests
{
    [Fact]
    public void Config_valida_se_construye()
    {
        var c = new PomodoroConfig(TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), 4);
        Assert.Equal(TimeSpan.FromMinutes(25), c.Focus);
        Assert.Equal(4, c.FocusesPerLongBreak);
    }

    [Fact]
    public void Focus_cero_o_negativo_lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PomodoroConfig(TimeSpan.Zero, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), 4));
    }

    [Fact]
    public void FocusesPerLongBreak_menor_que_uno_lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PomodoroConfig(TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), 0));
    }

    [Fact]
    public void Presets_tienen_valores_esperados()
    {
        Assert.Equal(TimeSpan.FromMinutes(25), PomodoroConfig.Classic.Focus);
        Assert.Equal(TimeSpan.FromMinutes(50), PomodoroConfig.DeepWork.Focus);
        Assert.Equal(2, PomodoroConfig.DeepWork.FocusesPerLongBreak);
    }
}

public class PomodoroEngineTests
{
    // Config corta y fácil de razonar: foco 25m, corto 5m, largo 15m, largo cada 4 focos.
    private static PomodoroEngine NewEngine() => new(PomodoroConfig.Classic);
    private static readonly DateTime T0 = new(2026, 6, 1, 9, 0, 0);

    [Fact]
    public void Arranca_en_Idle()
    {
        var e = NewEngine();
        Assert.Equal(PomodoroPhase.Idle, e.Phase);
        Assert.False(e.IsRunning);
    }

    [Fact]
    public void Start_entra_en_Focus_corriendo()
    {
        var e = NewEngine();
        e.Start(T0);
        Assert.Equal(PomodoroPhase.Focus, e.Phase);
        Assert.True(e.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(25), e.CurrentPhaseDuration);
    }

    [Fact]
    public void Remaining_decrece_con_el_tiempo()
    {
        var e = NewEngine();
        e.Start(T0);
        Assert.Equal(TimeSpan.FromMinutes(25), e.Remaining(T0));
        Assert.Equal(TimeSpan.FromMinutes(15), e.Remaining(T0.AddMinutes(10)));
        Assert.Equal(TimeSpan.Zero, e.Remaining(T0.AddMinutes(25)));
        // No baja de cero aunque pase de largo.
        Assert.Equal(TimeSpan.Zero, e.Remaining(T0.AddMinutes(40)));
    }

    [Fact]
    public void Advance_sin_completar_no_transiciona()
    {
        var e = NewEngine();
        e.Start(T0);
        var r = e.Advance(T0.AddMinutes(10));
        Assert.False(r.PhaseCompleted);
        Assert.Equal(PomodoroPhase.Focus, e.Phase);
    }

    [Fact]
    public void Advance_al_completar_Focus_pasa_a_ShortBreak()
    {
        var e = NewEngine();
        e.Start(T0);
        var r = e.Advance(T0.AddMinutes(25));
        Assert.True(r.PhaseCompleted);
        Assert.Equal(PomodoroPhase.Focus, r.CompletedPhase);
        Assert.Equal(PomodoroPhase.ShortBreak, r.NewPhase);
        Assert.Equal(1, e.CompletedFocuses);
    }

    [Fact]
    public void Cuarto_Focus_lleva_a_LongBreak()
    {
        var e = NewEngine();
        var t = T0;
        e.Start(t);
        // Completar 4 ciclos foco+descanso. Tras el 4º foco -> LongBreak.
        for (int i = 1; i <= 4; i++)
        {
            // completar Focus
            t = t.AddMinutes(25);
            var rf = e.Advance(t);
            Assert.Equal(PomodoroPhase.Focus, rf.CompletedPhase);
            if (i < 4)
            {
                Assert.Equal(PomodoroPhase.ShortBreak, rf.NewPhase);
                // completar ShortBreak (5m)
                t = t.AddMinutes(5);
                var rb = e.Advance(t);
                Assert.Equal(PomodoroPhase.Focus, rb.NewPhase);
            }
            else
            {
                Assert.Equal(PomodoroPhase.LongBreak, rf.NewPhase);
            }
        }
        Assert.Equal(4, e.CompletedFocuses);
        Assert.Equal(PomodoroPhase.LongBreak, e.Phase);
    }

    [Fact]
    public void LongBreak_vuelve_a_Focus()
    {
        var e = NewEngine();
        var t = T0;
        e.Start(t);
        // Avanzar hasta LongBreak (4 focos).
        for (int i = 1; i <= 4; i++)
        {
            t = t.AddMinutes(25); e.Advance(t);
            if (i < 4) { t = t.AddMinutes(5); e.Advance(t); }
        }
        Assert.Equal(PomodoroPhase.LongBreak, e.Phase);
        // Completar el descanso largo (15m) -> Focus.
        t = t.AddMinutes(15);
        var r = e.Advance(t);
        Assert.Equal(PomodoroPhase.LongBreak, r.CompletedPhase);
        Assert.Equal(PomodoroPhase.Focus, r.NewPhase);
    }

    [Fact]
    public void Pause_congela_el_tiempo_restante()
    {
        var e = NewEngine();
        e.Start(T0);
        // A los 10 min, pausa. Quedan 15.
        e.Pause(T0.AddMinutes(10));
        Assert.False(e.IsRunning);
        // Aunque pasen 30 min reales en pausa, sigue quedando 15.
        Assert.Equal(TimeSpan.FromMinutes(15), e.Remaining(T0.AddMinutes(40)));
    }

    [Fact]
    public void Resume_continua_donde_se_quedo()
    {
        var e = NewEngine();
        e.Start(T0);
        e.Pause(T0.AddMinutes(10));         // consumidos 10, quedan 15
        e.Resume(T0.AddMinutes(40));        // reanuda 30 min después
        Assert.True(e.IsRunning);
        // 5 min tras reanudar -> consumidos 15, quedan 10.
        Assert.Equal(TimeSpan.FromMinutes(10), e.Remaining(T0.AddMinutes(45)));
        // Completa a los +15 desde la reanudación (10 previos + 15 = 25).
        var r = e.Advance(T0.AddMinutes(55));
        Assert.True(r.PhaseCompleted);
        Assert.Equal(PomodoroPhase.ShortBreak, r.NewPhase);
    }

    [Fact]
    public void Skip_salta_de_fase_inmediatamente()
    {
        var e = NewEngine();
        e.Start(T0);
        var r = e.Skip(T0.AddMinutes(3)); // saltar el foco a los 3 min
        Assert.True(r.PhaseCompleted);
        Assert.Equal(PomodoroPhase.ShortBreak, r.NewPhase);
        Assert.Equal(1, e.CompletedFocuses); // saltar un foco lo cuenta
    }

    [Fact]
    public void Reset_vuelve_a_Idle_y_resetea_focos()
    {
        var e = NewEngine();
        e.Start(T0);
        e.Advance(T0.AddMinutes(25)); // 1 foco
        e.Reset();
        Assert.Equal(PomodoroPhase.Idle, e.Phase);
        Assert.False(e.IsRunning);
        Assert.Equal(0, e.CompletedFocuses);
        Assert.Equal(TimeSpan.Zero, e.Remaining(T0.AddMinutes(30)));
    }

    [Fact]
    public void En_Idle_los_controles_no_hacen_nada()
    {
        var e = NewEngine();
        e.Pause(T0);
        e.Resume(T0);
        var r = e.Skip(T0);
        Assert.Equal(PomodoroPhase.Idle, e.Phase);
        Assert.False(r.PhaseCompleted);
    }
}
