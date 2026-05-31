using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Ritmo.Core.Model;
using Ritmo_App.Services;

namespace Ritmo_App.Dialogs;

/// <summary>
/// Diálogo para crear o editar una categoría de bloque (#83): nombre, color (de la paleta
/// curada <see cref="SchedulePalette"/>) y si dispara concentración. La persistencia y la
/// generación del id (slug) las hace <c>ConfigurationService.AddCategory/UpdateCategory</c>.
/// </summary>
public sealed partial class CategoryDialog : ContentDialog
{
    private string _selectedHex = "#00897B";   // teal por defecto (neutro, no «estudio»)

    public CategoryDialog()
    {
        InitializeComponent();
        BuildSwatches();
    }

    /// <summary>Nombre escrito (vacío => el comando lo rechaza con mensaje).</summary>
    public string CategoryName => NameBox.Text?.Trim() ?? "";

    /// <summary>Color elegido en "#RRGGBB".</summary>
    public string SelectedColorHex => _selectedHex;

    /// <summary>¿Es de concentración?</summary>
    public bool IsFocus => FocusSwitch.IsOn;

    /// <summary>Carga una categoría existente para editarla.</summary>
    public void LoadFrom(BlockCategory c)
    {
        Title = "Editar categoría";
        NameBox.Text = c.Name;
        FocusSwitch.IsOn = c.IsFocus;
        _selectedHex = Normalize(c.ColorHex) ?? _selectedHex;
        BuildSwatches();   // re-pinta con la selección correcta
    }

    /// <summary>Valores para una categoría nueva.</summary>
    public void LoadDefaults()
    {
        Title = "Nueva categoría";
        FocusSwitch.IsOn = false;
        BuildSwatches();
    }

    // ---------- Paleta de colores (misma rejilla curada que Ajustes → Colores) ----------

    private void BuildSwatches()
    {
        SwatchHost.Children.Clear();
        SwatchHost.ColumnDefinitions.Clear();
        SwatchHost.RowDefinitions.Clear();

        var cols = SchedulePalette.Columns();
        int nRows = cols.Count > 0 ? cols[0].Count : 0;
        for (int c = 0; c < cols.Count; c++) SwatchHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int r = 0; r < nRows; r++) SwatchHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var stroke = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
        var accent = new SolidColorBrush(((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color);

        for (int c = 0; c < cols.Count; c++)
            for (int r = 0; r < cols[c].Count; r++)
            {
                var hex = cols[c][r];
                bool isSel = string.Equals(hex, _selectedHex, StringComparison.OrdinalIgnoreCase);
                var sw = new Button
                {
                    Width = 24, Height = 22, MinWidth = 0, Padding = new Thickness(0),
                    Background = new SolidColorBrush(ParseHex(hex)),
                    CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(isSel ? 2 : 1),
                    BorderBrush = isSel ? accent : stroke
                };
                ToolTipService.SetToolTip(sw, hex);
                Grid.SetColumn(sw, c); Grid.SetRow(sw, r);
                var thisHex = hex;
                sw.Click += (_, _) => { _selectedHex = thisHex; BuildSwatches(); };
                SwatchHost.Children.Add(sw);
            }
    }

    private static string? Normalize(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var h = hex.Trim().TrimStart('#');
        return h.Length == 6 ? "#" + h.ToUpperInvariant() : null;
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
