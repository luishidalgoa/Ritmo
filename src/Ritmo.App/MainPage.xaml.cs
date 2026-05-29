using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Pomodoro;
using Ritmo.Core.Timing;

namespace Ritmo_App;

/// <summary>
/// Página de bienvenida. De momento demuestra que la UI consume el núcleo
/// (Ritmo.Core): lee el preset Pomodoro y crea un motor con el reloj del sistema.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();

        // Datos reales tomados del núcleo, no texto hardcodeado.
        var config = PomodoroConfig.DeepWork;
        PomodoroInfo.Text =
            $"Concentración {config.Focus.TotalMinutes:0} min · " +
            $"descanso {config.ShortBreak.TotalMinutes:0} min · " +
            $"largo {config.LongBreak.TotalMinutes:0} min cada {config.FocusesPerLongBreak} focos.";

        IClock clock = new SystemClock();
        var engine = new PomodoroEngine(config);
        EngineState.Text = $"Motor listo · fase actual: {engine.Phase} · {clock.Now:dddd HH:mm}";
    }
}
