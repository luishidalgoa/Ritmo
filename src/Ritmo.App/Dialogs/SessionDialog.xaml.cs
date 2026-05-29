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
    private (ToggleButton btn, DayOfWeek day)[] _dayToggles = [];

    public SessionDialog()
    {
        InitializeComponent();
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

    // ---------- Avisos previos: dos desplegables con presets + personalizado (#87) ----------

    private void Alert1Box_SelectionChanged(object sender, SelectionChangedEventArgs e) => SyncCustom(Alert1Box, Alert1Custom);
    private void Alert2Box_SelectionChanged(object sender, SelectionChangedEventArgs e) => SyncCustom(Alert2Box, Alert2Custom);

    private static void SyncCustom(ComboBox box, NumberBox custom)
    {
        bool isCustom = (box.SelectedItem as ComboBoxItem)?.Tag as string == "custom";
        custom.Visibility = isCustom ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        if (isCustom && double.IsNaN(custom.Value)) custom.Value = 20;
    }

    /// <summary>Minutos del desplegable (0 = ninguno; "custom" = el NumberBox).</summary>
    private static int AlertMinutes(ComboBox box, NumberBox custom)
    {
        if (box.SelectedItem is not ComboBoxItem it) return 0;
        var tag = (string)it.Tag;
        if (tag == "custom") return double.IsNaN(custom.Value) ? 0 : (int)custom.Value;
        return int.TryParse(tag, out var m) ? m : 0;
    }

    /// <summary>Coloca un valor de minutos en un desplegable (preset si coincide, si no "Personalizado").</summary>
    private static void SetAlert(ComboBox box, NumberBox custom, int minutes)
    {
        if (minutes <= 0) { box.SelectedIndex = 0; SyncCustom(box, custom); return; }
        foreach (var item in box.Items.OfType<ComboBoxItem>())
            if ((string)item.Tag != "custom" && int.TryParse((string)item.Tag, out var m) && m == minutes)
            { box.SelectedItem = item; SyncCustom(box, custom); return; }
        // No es un preset: Personalizado con el valor.
        box.SelectedItem = box.Items.OfType<ComboBoxItem>().First(i => (string)i.Tag == "custom");
        custom.Value = minutes;
        SyncCustom(box, custom);
    }

    private void SetDays(IEnumerable<DayOfWeek> days)
    {
        var set = days.ToHashSet();
        foreach (var (btn, day) in _dayToggles) btn.IsChecked = set.Contains(day);
    }

    /// <summary>Marca un conjunto de días (p. ej. todos los de un grupo fusionado).</summary>
    public void PreselectDays(IEnumerable<DayOfWeek> days) => SetDays(days);

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

        // Hasta 2 avisos en los dos desplegables (ordenados de mayor a menor).
        var mins = s.PreAlerts.Select(a => a.MinutesBefore).Where(m => m > 0).Distinct().OrderByDescending(m => m).ToList();
        SetAlert(Alert1Box, Alert1Custom, mins.Count > 0 ? mins[0] : 0);
        SetAlert(Alert2Box, Alert2Custom, mins.Count > 1 ? mins[1] : 0);
    }

    /// <summary>Valores por defecto para una sesión nueva (día/hora opcionales).</summary>
    public void LoadDefaults(DayOfWeek? day = null, TimeOnly? start = null)
    {
        SetDays(day is null ? [] : [day.Value]);
        var st = start ?? new TimeOnly(9, 0);
        StartPicker.Time = st.ToTimeSpan();
        EndPicker.Time = st.Add(TimeSpan.FromHours(1)).ToTimeSpan();
        KindBox.SelectedIndex = 0;
        SetAlert(Alert1Box, Alert1Custom, 10);   // aviso por defecto: 10 minutos antes
        SetAlert(Alert2Box, Alert2Custom, 0);    // segundo aviso: ninguno
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

        var selected = new[] { AlertMinutes(Alert1Box, Alert1Custom), AlertMinutes(Alert2Box, Alert2Custom) };
        var alerts = PreAlertPresets.Compose(selected);

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
