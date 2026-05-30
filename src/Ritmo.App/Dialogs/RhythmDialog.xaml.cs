using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Pomodoro;

namespace Ritmo_App.Dialogs;

/// <summary>Diálogo para crear o editar un ritmo Pomodoro propio. #96</summary>
public sealed partial class RhythmDialog : ContentDialog
{
    public RhythmDialog()
    {
        InitializeComponent();
        FocusBox.Value = 25;
        ShortBox.Value = 5;
        LongBox.Value = 15;
        CyclesBox.Value = 4;
    }

    public string RhythmName => string.IsNullOrWhiteSpace(NameBox.Text) ? "Ritmo" : NameBox.Text.Trim();
    public int FocusMin => Val(FocusBox, 25);
    public int ShortMin => Val(ShortBox, 5);
    public int LongMin => Val(LongBox, 15);
    public int FocusesPerLong => Val(CyclesBox, 4);

    /// <summary>Carga un ritmo existente para editarlo.</summary>
    public void LoadFrom(PomodoroRhythm r)
    {
        Title = "Editar ritmo Pomodoro";
        NameBox.Text = r.Name;
        FocusBox.Value = r.FocusMinutes;
        ShortBox.Value = r.ShortBreakMinutes;
        LongBox.Value = r.LongBreakMinutes;
        CyclesBox.Value = r.FocusesPerLongBreak;
    }

    private static int Val(NumberBox b, int fallback) => double.IsNaN(b.Value) ? fallback : (int)b.Value;
}
