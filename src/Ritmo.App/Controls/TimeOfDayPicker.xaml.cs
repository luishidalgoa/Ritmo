using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ritmo.Core.Model;

namespace Ritmo_App.Controls;

/// <summary>
/// Selector de hora del día (HH:mm) en dos columnas (Hora | Min). A diferencia del
/// <c>TimePicker</c> nativo, puede tener un mínimo EXCLUSIVO (#150): los slots que NO lo
/// superan se pintan en gris (deshabilitados) y no se pueden elegir. Se usa para que la
/// hora de fin nunca pueda ser anterior ni igual a la de inicio.
/// </summary>
public sealed partial class TimeOfDayPicker : UserControl
{
    private readonly List<Button> _hourBtns = new();
    private readonly List<Button> _minuteBtns = new();
    private int _hour = 9, _minute;
    private bool _built;

    /// <summary>Se dispara cuando el usuario (o un ajuste de mínimo) cambia la hora elegida.</summary>
    public event EventHandler? TimeChanged;

    public TimeOfDayPicker()
    {
        InitializeComponent();
        Loaded += (_, _) => { BuildColumns(); Refresh(); };
        DropFlyout.Opened += (_, _) => BringSelectionIntoView();
    }

    // Paso de minutos (5 en sesiones, 30 en el rango del día). Se fija en XAML antes de Loaded.
    public static readonly DependencyProperty MinuteStepProperty = DependencyProperty.Register(
        nameof(MinuteStep), typeof(int), typeof(TimeOfDayPicker), new PropertyMetadata(5));
    public int MinuteStep { get => (int)GetValue(MinuteStepProperty); set => SetValue(MinuteStepProperty, value); }

    /// <summary>
    /// Etiqueta opcional encima del control (p. ej. "Inicio" / "Fin"). Es una DependencyProperty
    /// para que x:Uid pueda localizarla desde los .resw (#48 i18n).
    /// </summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(TimeOfDayPicker),
        new PropertyMetadata(null, OnHeaderChanged));
    public string? Header { get => (string?)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (TimeOfDayPicker)d;
        var value = e.NewValue as string;
        ctrl.HeaderText.Text = value ?? "";
        ctrl.HeaderText.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
    }

    private TimeSpan? _minExclusive;
    /// <summary>Mínimo exclusivo: la hora elegida debe superarlo. null = sin restricción.</summary>
    public TimeSpan? MinExclusive
    {
        get => _minExclusive;
        set
        {
            _minExclusive = value;
            if (value is { } min && Time <= min) SetTime(SnapAbove(min), raise: true);   // hora actual quedó inválida
            else Refresh();
        }
    }

    public TimeSpan Time
    {
        get => new(_hour, _minute, 0);
        set => SetTime(value, raise: false);
    }

    private int Step => MinuteStep < 1 ? 1 : MinuteStep;
    private TimeOnly? MinTime => _minExclusive is { } m ? TimeOnly.FromTimeSpan(m) : null;

    private void SetTime(TimeSpan t, bool raise)
    {
        _hour = Math.Clamp(t.Hours, 0, 23);
        _minute = (Math.Clamp((int)t.TotalMinutes % 60, 0, 59) / Step) * Step;   // ajusta al paso
        UpdateValueText();
        Refresh();
        if (raise) TimeChanged?.Invoke(this, EventArgs.Empty);
    }

    private TimeSpan SnapAbove(TimeSpan min)
    {
        var t = new TimeSpan(min.Hours, (min.Minutes / Step) * Step, 0) + TimeSpan.FromMinutes(Step);
        var dayMax = TimeSpan.FromHours(24) - TimeSpan.FromMinutes(Step);
        return t > dayMax ? dayMax : t;
    }

    private void UpdateValueText()
    {
        ValueText.Text = $"{_hour:00}:{_minute:00}";
        var label = string.IsNullOrEmpty(Header) ? "Hora" : Header;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(Trigger, $"{label} {_hour:00}:{_minute:00}");
    }

    private void BuildColumns()
    {
        if (_built) return;
        _built = true;
        for (int h = 0; h < 24; h++)
        {
            var b = ColumnButton($"{h:00}", h);
            b.Click += (s, _) => { _hour = (int)((Button)s).Tag; AfterHourPicked(); };
            _hourBtns.Add(b);
            HoursPanel.Children.Add(b);
        }
        for (int m = 0; m < 60; m += Step)
        {
            var b = ColumnButton($"{m:00}", m);
            b.Click += (s, _) => { _minute = (int)((Button)s).Tag; AfterMinutePicked(); };
            _minuteBtns.Add(b);
            MinutesPanel.Children.Add(b);
        }
    }

    private static Button ColumnButton(string text, int tag) => new()
    {
        Content = text,
        Tag = tag,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        BorderThickness = new Thickness(0),
        Margin = new Thickness(1),
        Padding = new Thickness(0, 6, 0, 6)
    };

    private void AfterHourPicked()
    {
        // Si el minuto actual no es válido para la nueva hora, súbelo al primero válido.
        if (!TimeSlots.MinuteEnabled(_hour, _minute, MinTime))
            _minute = TimeSlots.FirstValidMinute(_hour, Step, MinTime);
        UpdateValueText();
        Refresh();
        TimeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AfterMinutePicked()
    {
        UpdateValueText();
        Refresh();
        TimeChanged?.Invoke(this, EventArgs.Empty);
        DropFlyout.Hide();   // elegir el minuto completa la selección
    }

    private void Refresh()
    {
        if (!_built) return;
        var min = MinTime;
        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        foreach (var b in _hourBtns)
        {
            int h = (int)b.Tag;
            b.IsEnabled = TimeSlots.HourEnabled(h, Step, min);
            b.Background = h == _hour ? accent : transparent;
        }
        foreach (var b in _minuteBtns)
        {
            int m = (int)b.Tag;
            b.IsEnabled = TimeSlots.MinuteEnabled(_hour, m, min);
            b.Background = m == _minute ? accent : transparent;
        }
    }

    private void BringSelectionIntoView()
    {
        foreach (var b in _hourBtns) if ((int)b.Tag == _hour) { b.StartBringIntoView(); break; }
        foreach (var b in _minuteBtns) if ((int)b.Tag == _minute) { b.StartBringIntoView(); break; }
    }
}
