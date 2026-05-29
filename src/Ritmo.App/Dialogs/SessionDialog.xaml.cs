using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Ritmo.Core.Model;

namespace Ritmo_App.Dialogs;

/// <summary>
/// Diálogo para crear o editar una sesión del horario. Permite elegir VARIOS días
/// a la vez (#81) y fijar inicio + fin (#80, la duración se calcula).
/// </summary>
public sealed partial class SessionDialog : ContentDialog
{
    private const int MaxAlerts = 2;   // spec: hasta 2 avisos previos

    private IReadOnlyList<int> _preservedAlerts = [];
    private bool _suppressAlertCheck;
    private List<CheckBox> _alertBoxes = [];
    private (ToggleButton btn, DayOfWeek day)[] _dayToggles = [];

    public SessionDialog()
    {
        InitializeComponent();
        _alertBoxes = [Alert60, Alert10, Alert5];
        foreach (var cb in _alertBoxes)
            cb.Checked += AlertBox_Checked;

        _dayToggles =
        [
            (DayMon, DayOfWeek.Monday), (DayTue, DayOfWeek.Tuesday), (DayWed, DayOfWeek.Wednesday),
            (DayThu, DayOfWeek.Thursday), (DayFri, DayOfWeek.Friday), (DaySat, DayOfWeek.Saturday),
            (DaySun, DayOfWeek.Sunday)
        ];
    }

    /// <summary>Días marcados (uno o varios). El que crea la sesión itera sobre ellos.</summary>
    public IReadOnlyList<DayOfWeek> SelectedDays =>
        _dayToggles.Where(t => t.btn.IsChecked == true).Select(t => t.day).ToList();

    private static int MinutesOf(CheckBox cb) => cb.Name switch
    {
        nameof(Alert60) => 60,
        nameof(Alert10) => 10,
        _ => 5
    };

    private void AlertBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_suppressAlertCheck) return;
        if (_alertBoxes.Count(b => b.IsChecked == true) <= MaxAlerts) return;
        _suppressAlertCheck = true;
        ((CheckBox)sender).IsChecked = false;
        _suppressAlertCheck = false;
        AlertHint.Text = $"Máximo {MaxAlerts} avisos";
    }

    private void SetDays(IEnumerable<DayOfWeek> days)
    {
        var set = days.ToHashSet();
        foreach (var (btn, day) in _dayToggles) btn.IsChecked = set.Contains(day);
    }

    /// <summary>Rellena el diálogo con una sesión existente (para editar).</summary>
    public void LoadFrom(StudySession s)
    {
        TitleBox.Text = s.Title;
        SetDays([s.Day]);
        DayHint.Text = "Al editar, cambia el día marcándolo aquí.";
        StartPicker.Time = s.Start.ToTimeSpan();
        EndPicker.Time = s.End.ToTimeSpan();
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

    /// <summary>Valores por defecto para una sesión nueva (día/hora opcionales).</summary>
    public void LoadDefaults(DayOfWeek? day = null, TimeOnly? start = null)
    {
        SetDays(day is null ? [] : [day.Value]);
        var st = start ?? new TimeOnly(9, 0);
        StartPicker.Time = st.ToTimeSpan();
        EndPicker.Time = st.Add(TimeSpan.FromHours(1)).ToTimeSpan();
        KindBox.SelectedIndex = 0;
        _suppressAlertCheck = true;
        Alert10.IsChecked = true;   // aviso por defecto: 10 minutos antes
        _suppressAlertCheck = false;
    }

    /// <summary>Construye la sesión para un día concreto (inicio+fin → duración).</summary>
    public StudySession ToSession(DayOfWeek day)
    {
        var start = TimeOnly.FromTimeSpan(StartPicker.Time);
        var end = TimeOnly.FromTimeSpan(EndPicker.Time);
        var duration = ScheduleMath.DurationBetween(start, end);

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
            Duration = duration,
            Kind = kind,
            PreAlerts = alerts,
            IsTentative = TentativeSwitch.IsOn
        };
    }
}
