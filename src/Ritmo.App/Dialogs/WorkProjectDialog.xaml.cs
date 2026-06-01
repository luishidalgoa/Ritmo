using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Ritmo.Core.Model;

namespace Ritmo_App.Dialogs;

/// <summary>Diálogo para crear o editar un proyecto de seguimiento laboral (#84 V3).</summary>
public sealed partial class WorkProjectDialog : ContentDialog
{
    // Paleta de colores para proyectos (tonos saturados, distinguibles entre sí).
    private static readonly string[] Palette =
    {
        "#1E88E5", "#43A047", "#E53935", "#FB8C00", "#8E24AA",
        "#00897B", "#D81B60", "#3949AB", "#7CB342", "#F4511E", "#546E7A"
    };

    private string _color = "#1E88E5";

    public WorkProjectDialog()
    {
        InitializeComponent();
    }

    public string ProjectName => NameBox.Text?.Trim() ?? "";
    public decimal Rate => double.IsNaN(RateBox.Value) ? 0m : (decimal)RateBox.Value;
    public double GoalHours => double.IsNaN(GoalBox.Value) ? 0 : GoalBox.Value;
    public string ColorHex => _color;
    public string CurrencyCode => (CurrencyBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "EUR";
    public bool AutoFromSchedule => AutoSwitch.IsOn;

    public void LoadDefaults()
    {
        Title = "Nuevo proyecto";
        CurrencyBox.SelectedIndex = 0;
        AutoSwitch.IsOn = true;
        _color = Palette[0];
        BuildSwatches();
    }

    public void LoadFrom(WorkProject p)
    {
        Title = "Editar proyecto";
        NameBox.Text = p.Name;
        RateBox.Value = (double)p.Rate;
        GoalBox.Value = p.MonthlyGoalHours;
        AutoSwitch.IsOn = p.AutoFromSchedule;
        _color = string.IsNullOrWhiteSpace(p.ColorHex) ? Palette[0] : p.ColorHex;
        SelectCurrency(p.CurrencyCode);
        BuildSwatches();
    }

    private void SelectCurrency(string code)
    {
        for (int i = 0; i < CurrencyBox.Items.Count; i++)
            if (CurrencyBox.Items[i] is ComboBoxItem it && (string)it.Tag == code) { CurrencyBox.SelectedIndex = i; return; }
        CurrencyBox.SelectedIndex = 0;
    }

    private void BuildSwatches()
    {
        SwatchHost.Children.Clear();
        SwatchHost.ColumnDefinitions.Clear();
        SwatchHost.RowDefinitions.Clear();
        const int cols = 6;
        for (int c = 0; c < cols; c++) SwatchHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var accent = new SolidColorBrush(((SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"]).Color);
        var stroke = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];

        for (int i = 0; i < Palette.Length; i++)
        {
            int r = i / cols, c = i % cols;
            while (SwatchHost.RowDefinitions.Count <= r) SwatchHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var hex = Palette[i];
            bool sel = string.Equals(hex, _color, StringComparison.OrdinalIgnoreCase);
            var sw = new Button
            {
                Width = 30, Height = 30, MinWidth = 0, Padding = new Thickness(0),
                Background = new SolidColorBrush(ParseHex(hex)),
                CornerRadius = new CornerRadius(15),
                BorderThickness = new Thickness(sel ? 3 : 1),
                BorderBrush = sel ? accent : stroke
            };
            Grid.SetColumn(sw, c); Grid.SetRow(sw, r);
            var thisHex = hex;
            sw.Click += (_, _) => { _color = thisHex; BuildSwatches(); };
            SwatchHost.Children.Add(sw);
        }
    }

    private static Color ParseHex(string hex)
    {
        var h = hex.TrimStart('#');
        return Color.FromArgb(255,
            Convert.ToByte(h.Substring(0, 2), 16),
            Convert.ToByte(h.Substring(2, 2), 16),
            Convert.ToByte(h.Substring(4, 2), 16));
    }
}
