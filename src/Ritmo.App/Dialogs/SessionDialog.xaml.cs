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
        AlertHelp.Content = Ritmo_App.Services.HelpHint.Icon("prealert");   // ayuda (#93)
    }

    /// <summary>Carga los títulos existentes del horario como sugerencias del combo (#116).</summary>
    public void SetKnownTitles(IEnumerable<string> titles)
    {
        var current = TitleBox.Text;
        TitleBox.Items.Clear();
        foreach (var t in titles) TitleBox.Items.Add(t);
        TitleBox.Text = current;   // no perder lo escrito al rellenar las sugerencias
    }

    /// <summary>
    /// Llena el desplegable de categorías desde los ajustes del usuario (#83). Debe llamarse
    /// ANTES de <see cref="LoadFrom"/>/<see cref="LoadDefaults"/> para que la preselección
    /// case. Content = nombre visible, Tag = id estable de la categoría.
    /// </summary>
    public void SetCategories(IEnumerable<BlockCategory> categories)
    {
        KindBox.Items.Clear();
        foreach (var c in categories.OrderBy(c => c.Order))
            KindBox.Items.Add(new ComboBoxItem { Content = c.Name, Tag = c.Id });
    }

    /// <summary>Días marcados (uno o varios). El que crea la sesión itera sobre ellos.</summary>
    public IReadOnlyList<DayOfWeek> SelectedDays =>
        _dayToggles.Where(t => t.btn.IsChecked == true).Select(t => t.day).ToList();

    /// <summary>¿Es una sesión extraordinaria con fecha(s) concreta(s)? (#103/#131)</summary>
    public bool IsOneOff => OneOffSwitch.IsOn;

    /// <summary>Fecha de inicio del rango provisional (#131).</summary>
    public DateOnly StartDate =>
        StartDatePicker.Date is { } d ? DateOnly.FromDateTime(d.Date) : DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Fecha de fin INCLUSIVE; si está vacía o es anterior al inicio, = inicio (#131).</summary>
    public DateOnly EndDate
    {
        get
        {
            var start = StartDate;
            if (EndDatePicker.Date is { } d) { var e = DateOnly.FromDateTime(d.Date); return e < start ? start : e; }
            return start;
        }
    }

    /// <summary>Pre-marca/desmarca «extraordinaria» (al editar una sesión provisional). #103</summary>
    public void SetOneOff(bool on)
    {
        OneOffSwitch.IsOn = on;
        UpdateOneOffVisibility();
    }

    /// <summary>Pre-rellena el rango de fechas de la sesión provisional. #131</summary>
    public void SetOneOffDates(DateOnly start, DateOnly end)
    {
        StartDatePicker.Date = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue));
        EndDatePicker.Date = new DateTimeOffset((end < start ? start : end).ToDateTime(TimeOnly.MinValue));
    }

    private void OneOffSwitch_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => UpdateOneOffVisibility();

    private void UpdateOneOffVisibility()
    {
        bool on = OneOffSwitch.IsOn;
        DaysPanel.Visibility = on ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        OneOffDatesPanel.Visibility = on ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        if (on && StartDatePicker.Date is null)   // al activar sin fechas, por defecto hoy
        {
            var today = new DateTimeOffset(DateTime.Today);
            StartDatePicker.Date = today;
            EndDatePicker.Date = today;
        }
    }

    /// <summary>Mantén «Hasta» ≥ «Desde»: si queda antes o vacía, la igualamos al inicio.</summary>
    private void StartDatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (StartDatePicker.Date is { } s && (EndDatePicker.Date is null || EndDatePicker.Date < s))
            EndDatePicker.Date = s;
    }

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
            if (KindBox.Items[i] is ComboBoxItem it && (string)it.Tag == s.CategoryId)
            { KindBox.SelectedIndex = i; break; }

        // Hasta 2 avisos en los dos desplegables (ordenados de mayor a menor).
        var mins = s.PreAlerts.Select(a => a.MinutesBefore).Where(m => m > 0).Distinct().OrderByDescending(m => m).ToList();
        SetAlert(Alert1Box, Alert1Custom, mins.Count > 0 ? mins[0] : 0);
        SetAlert(Alert2Box, Alert2Custom, mins.Count > 1 ? mins[1] : 0);
    }

    /// <summary>
    /// Valores por defecto para una sesión nueva (día/hora opcionales). El aviso previo inicial
    /// lo decide el ajuste del usuario (#48); 0 = sin aviso por defecto.
    /// </summary>
    public void LoadDefaults(DayOfWeek? day = null, TimeOnly? start = null, int defaultPreAlertMinutes = 10)
    {
        SetDays(day is null ? [] : [day.Value]);
        var st = start ?? new TimeOnly(9, 0);
        StartPicker.Time = st.ToTimeSpan();
        EndPicker.Time = st.Add(TimeSpan.FromHours(1)).ToTimeSpan();
        KindBox.SelectedIndex = 0;
        SetAlert(Alert1Box, Alert1Custom, defaultPreAlertMinutes);   // aviso por defecto configurable (#48)
        SetAlert(Alert2Box, Alert2Custom, 0);                        // segundo aviso: ninguno
    }

    /// <summary>Construye la sesión para un día concreto (inicio+fin → duración).</summary>
    public StudySession ToSession(DayOfWeek day)
    {
        var start = TimeOnly.FromTimeSpan(StartPicker.Time);
        var end = TimeOnly.FromTimeSpan(EndPicker.Time);
        var duration = ScheduleMath.DurationBetween(start, end);

        var categoryId = (KindBox.SelectedItem as ComboBoxItem)?.Tag as string;
        if (string.IsNullOrWhiteSpace(categoryId)) categoryId = Ritmo.Core.Model.CategoryIds.Other;

        var selected = new[] { AlertMinutes(Alert1Box, Alert1Custom), AlertMinutes(Alert2Box, Alert2Custom) };
        var alerts = PreAlertPresets.Compose(selected);

        return new StudySession
        {
            Title = string.IsNullOrWhiteSpace(TitleBox.Text) ? "(sin título)" : TitleBox.Text.Trim(),
            Day = day,
            Start = start,
            Duration = duration,
            CategoryId = categoryId,
            PreAlerts = alerts,
            IsTentative = TentativeSwitch.IsOn
        };
    }
}
