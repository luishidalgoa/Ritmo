using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo_App.Services;

namespace Ritmo_App;

/// <summary>
/// Pantalla de Ajustes: configura el Pomodoro, el rango horario de la rejilla
/// y el tema. Persiste vía ConfigurationService (compartido con la UI y el MCP).
/// </summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadValues();
    }

    private void LoadValues()
    {
        var s = AppState.Load();
        FocusBox.Value = s.Pomodoro.Focus.TotalMinutes;
        ShortBox.Value = s.Pomodoro.ShortBreak.TotalMinutes;
        LongBox.Value = s.Pomodoro.LongBreak.TotalMinutes;
        CyclesBox.Value = s.Pomodoro.FocusesPerLongBreak;

        DayStartPicker.Time = s.ViewConfig.DayStart.ToTimeSpan();
        DayEndPicker.Time = s.ViewConfig.DayEnd.ToTimeSpan();

        // Tema actual de la ventana.
        ThemeBox.SelectedIndex = (this.ActualTheme) switch
        {
            ElementTheme.Light => 1,
            ElementTheme.Dark => 2,
            _ => 0
        };
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        int Val(NumberBox b, int fallback) => double.IsNaN(b.Value) ? fallback : (int)b.Value;

        var r1 = AppState.Config.SetPomodoro(
            Val(FocusBox, 50), Val(ShortBox, 10), Val(LongBox, 20), Val(CyclesBox, 2));

        var start = TimeOnly.FromTimeSpan(DayStartPicker.Time);
        var end = TimeOnly.FromTimeSpan(DayEndPicker.Time);
        var r2 = AppState.Config.SetViewHours(start, end);

        // Tema: aplicar en vivo a la raíz de la ventana.
        if (ThemeBox.SelectedItem is ComboBoxItem it && it.Tag is string tag)
        {
            var theme = tag switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
            if (this.XamlRoot?.Content is FrameworkElement root)
                root.RequestedTheme = theme;
        }

        SaveStatus.Text = (r1.Success && r2.Success)
            ? "✓ Guardado"
            : $"⚠ {(!r1.Success ? r1.Message : r2.Message)}";
    }
}
