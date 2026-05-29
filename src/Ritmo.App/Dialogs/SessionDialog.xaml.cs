using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Model;

namespace Ritmo_App.Dialogs;

/// <summary>Diálogo para crear o editar una sesión del horario.</summary>
public sealed partial class SessionDialog : ContentDialog
{
    private const int MaxAlerts = 2;   // spec: hasta 2 avisos previos

    private static readonly DayOfWeek[] Days =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
          DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

    // Avisos de la sesión que no son presets (p. ej. puestos por la IA): se preservan.
    private IReadOnlyList<int> _preservedAlerts = [];
    private bool _suppressAlertCheck;
    private List<CheckBox> _alertBoxes = [];

    public SessionDialog()
    {
        InitializeComponent();
        _alertBoxes = [Alert60, Alert10, Alert5];
        foreach (var cb in _alertBoxes)
            cb.Checked += AlertBox_Checked;
    }

    /// <summary>Minutos del preset asociado a cada casilla.</summary>
    private static int MinutesOf(CheckBox cb) => cb.Name switch
    {
        nameof(Alert60) => 60,
        nameof(Alert10) => 10,
        _ => 5
    };

    /// <summary>Impide marcar más de <see cref="MaxAlerts"/> avisos (revierte el de más).</summary>
    private void AlertBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_suppressAlertCheck) return;
        if (_alertBoxes.Count(b => b.IsChecked == true) <= MaxAlerts) return;

        _suppressAlertCheck = true;
        ((CheckBox)sender).IsChecked = false;     // revertir el tercero
        _suppressAlertCheck = false;
        AlertHint.Text = $"Máximo {MaxAlerts} avisos";
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

        var minutes = s.PreAlerts.Select(a => a.MinutesBefore).ToHashSet();
        _suppressAlertCheck = true;
        Alert60.IsChecked = minutes.Contains(60);
        Alert10.IsChecked = minutes.Contains(10);
        Alert5.IsChecked = minutes.Contains(5);
        _suppressAlertCheck = false;
        _preservedAlerts = PreAlertPresets.NonStandardOf(s.PreAlerts);
    }

    /// <summary>Valores por defecto para una sesión nueva (día/hora opcionales,
    /// los rellena el botón "+" de una celda concreta del calendario).</summary>
    public void LoadDefaults(DayOfWeek? day = null, TimeOnly? start = null)
    {
        DayBox.SelectedIndex = day is null ? 0 : Math.Max(0, Array.IndexOf(Days, day.Value));
        StartPicker.Time = (start ?? new TimeOnly(9, 0)).ToTimeSpan();
        DurationBox.Value = 60;
        KindBox.SelectedIndex = 0;
        // Aviso por defecto sensato para una sesión nueva: 10 minutos antes.
        _suppressAlertCheck = true;
        Alert10.IsChecked = true;
        _suppressAlertCheck = false;
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

        var selected = _alertBoxes.Where(b => b.IsChecked == true).Select(MinutesOf);
        var alerts = PreAlertPresets.Compose(selected, _preservedAlerts);

        return new StudySession
        {
            Title = string.IsNullOrWhiteSpace(TitleBox.Text) ? "(sin título)" : TitleBox.Text.Trim(),
            Day = day,
            Start = start,
            Duration = TimeSpan.FromMinutes(minutes),
            Kind = kind,
            PreAlerts = alerts,
            IsTentative = TentativeSwitch.IsOn
        };
    }
}
