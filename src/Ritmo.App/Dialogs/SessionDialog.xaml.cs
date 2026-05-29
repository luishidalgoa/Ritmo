using System;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Model;

namespace Ritmo_App.Dialogs;

/// <summary>Diálogo para crear o editar una sesión del horario.</summary>
public sealed partial class SessionDialog : ContentDialog
{
    private static readonly DayOfWeek[] Days =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
          DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

    public SessionDialog()
    {
        InitializeComponent();
    }

    /// <summary>Rellena el diálogo con una sesión existente (para editar).</summary>
    public void LoadFrom(StudySession s)
    {
        TitleBox.Text = s.Title;
        DayBox.SelectedIndex = Math.Max(0, Array.IndexOf(Days, s.Day));
        StartPicker.Time = s.Start.ToTimeSpan();
        DurationBox.Value = s.Duration.TotalMinutes;
        TentativeSwitch.IsOn = s.IsTentative;
        for (int i = 0; i < KindBox.Items.Count; i++)
            if (KindBox.Items[i] is ComboBoxItem it && (string)it.Tag == s.Kind.ToString())
            { KindBox.SelectedIndex = i; break; }
    }

    /// <summary>Valores por defecto para una sesión nueva.</summary>
    public void LoadDefaults()
    {
        DayBox.SelectedIndex = 0;
        StartPicker.Time = new TimeSpan(9, 0, 0);
        DurationBox.Value = 60;
        KindBox.SelectedIndex = 0;
    }

    /// <summary>Construye la sesión a partir de lo que el usuario introdujo.</summary>
    public StudySession ToSession()
    {
        var day = Days[Math.Max(0, DayBox.SelectedIndex)];
        var start = TimeOnly.FromTimeSpan(StartPicker.Time);
        var minutes = double.IsNaN(DurationBox.Value) ? 60 : DurationBox.Value;
        var kind = StudyKind.Otro;
        if (KindBox.SelectedItem is ComboBoxItem it && Enum.TryParse<StudyKind>((string)it.Tag, out var k))
            kind = k;

        return new StudySession
        {
            Title = string.IsNullOrWhiteSpace(TitleBox.Text) ? "(sin título)" : TitleBox.Text.Trim(),
            Day = day,
            Start = start,
            Duration = TimeSpan.FromMinutes(minutes),
            Kind = kind,
            IsTentative = TentativeSwitch.IsOn
        };
    }
}
