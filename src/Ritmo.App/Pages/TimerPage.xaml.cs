using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Pomodoro;

namespace Ritmo_App;

public sealed partial class TimerPage : Page
{
    public TimerPage()
    {
        InitializeComponent();

        var c = PomodoroConfig.DeepWork;
        PomodoroInfo.Text =
            $"Concentración {c.Focus.TotalMinutes:0} min · descanso {c.ShortBreak.TotalMinutes:0} min · " +
            $"largo {c.LongBreak.TotalMinutes:0} min cada {c.FocusesPerLongBreak} focos.";
    }
}
