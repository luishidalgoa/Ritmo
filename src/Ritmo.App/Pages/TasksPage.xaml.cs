using System;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Ritmo.Core.Model;
using Ritmo_App.Services;

namespace Ritmo_App;

/// <summary>
/// Página «Tareas» (#145, fase 2): listas de tareas estilo Recordatorios de Apple. Cada bloque es
/// una lista con su color; dentro, tareas con casilla de hecho, reordenables. CRUD inline. El
/// vínculo a entorno (cajones por sesión) y la sincronización externa quedan para fases siguientes.
/// </summary>
public sealed partial class TasksPage : Page
{
    // Paleta de colores para los bloques (presets, no hex libre).
    private static readonly string[] Palette =
        { "#E53935", "#FB8C00", "#FDD835", "#43A047", "#1E88E5", "#8E24AA", "#6D4C41", "#546E7A" };

    // Chevrons de Segoe Fluent (el enum Symbol no tiene «Down»).
    private const string ChevronUp = "";
    private const string ChevronDown = "";

    public TasksPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Build();
    }

    private void Build()
    {
        var s = AppState.Load();
        var blocks = s.TaskBlocks.OrderBy(b => b.Order).ToList();
        EmptyText.Visibility = blocks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        BlocksHost.Children.Clear();
        for (int i = 0; i < blocks.Count; i++)
            BlocksHost.Children.Add(BlockCard(s, blocks[i], i == 0, i == blocks.Count - 1));
    }

    private static Brush Hex(string? hex, double opacity = 1)
    {
        try
        {
            var h = (hex ?? "#1E88E5").TrimStart('#');
            var c = Windows.UI.Color.FromArgb(255,
                Convert.ToByte(h.Substring(0, 2), 16),
                Convert.ToByte(h.Substring(2, 2), 16),
                Convert.ToByte(h.Substring(4, 2), 16));
            return new SolidColorBrush(c) { Opacity = opacity };
        }
        catch { return (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]; }
    }

    private Button IconButton(Symbol symbol, string tooltip, Action onClick, bool enabled = true)
    {
        var b = new Button
        {
            Content = new SymbolIcon(symbol),
            MinWidth = 0, Padding = new Thickness(7),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            IsEnabled = enabled
        };
        ToolTipService.SetToolTip(b, tooltip);
        b.Click += (_, _) => onClick();
        return b;
    }

    private Button IconButtonGlyph(string glyph, string tooltip, Action onClick, bool enabled = true)
    {
        var b = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = 14 },
            MinWidth = 0, Padding = new Thickness(7),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            IsEnabled = enabled
        };
        ToolTipService.SetToolTip(b, tooltip);
        b.Click += (_, _) => onClick();
        return b;
    }

    private FrameworkElement BlockCard(Ritmo.Core.Persistence.AppSettings s, TaskBlock block, bool isFirst, bool isLast)
    {
        var tasks = s.Tasks.Where(t => t.BlockId == block.Id).OrderBy(t => t.Order).ToList();
        int pending = tasks.Count(t => !t.Done);

        var card = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1)
        };
        var stack = new StackPanel { Spacing = 10 };

        // --- Cabecera: punto de color + nombre + contador + acciones ---
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = Hex(block.ColorHex), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        Grid.SetColumn(dot, 0);

        var titleWrap = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        titleWrap.Children.Add(new TextBlock { Text = block.Name, FontSize = 18, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        titleWrap.Children.Add(new TextBlock { Text = pending == 0 ? "✓" : pending.ToString(), FontSize = 13, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(titleWrap, 1);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(IconButtonGlyph(ChevronUp, "Subir bloque", () => { AppState.Config.MoveTaskBlock(block.Id, true); Build(); }, !isFirst));
        actions.Children.Add(IconButtonGlyph(ChevronDown, "Bajar bloque", () => { AppState.Config.MoveTaskBlock(block.Id, false); Build(); }, !isLast));
        actions.Children.Add(ColorButton(block));
        actions.Children.Add(IconButton(Symbol.Rename, "Renombrar bloque", async () => await RenameBlock(block)));
        actions.Children.Add(IconButton(Symbol.Delete, "Eliminar bloque", async () => await ConfirmDeleteBlock(block)));
        Grid.SetColumn(actions, 2);

        header.Children.Add(dot); header.Children.Add(titleWrap); header.Children.Add(actions);
        stack.Children.Add(header);

        // --- Lista de tareas ---
        if (tasks.Count == 0)
            stack.Children.Add(new TextBlock { Text = "Sin tareas todavía.", Opacity = 0.5, FontSize = 13 });
        else
        {
            var list = new StackPanel { Spacing = 2 };
            for (int i = 0; i < tasks.Count; i++)
                list.Children.Add(TaskRow(tasks[i], i == 0, i == tasks.Count - 1));
            stack.Children.Add(list);
        }

        // --- Añadir tarea (input + botón) ---
        var addRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var input = new TextBox { PlaceholderText = "Nueva tarea…", Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(input, 0);
        void AddFromInput()
        {
            var text = (input.Text ?? "").Trim();
            if (text.Length == 0) return;
            AppState.Config.AddTask(block.Id, text);
            input.Text = "";
            Build();
        }
        input.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) AddFromInput(); };
        var addBtn = new Button { Content = "Añadir" };
        addBtn.Click += (_, _) => AddFromInput();
        Grid.SetColumn(addBtn, 1);
        addRow.Children.Add(input); addRow.Children.Add(addBtn);
        stack.Children.Add(addRow);

        card.Child = stack;
        return card;
    }

    private Button ColorButton(TaskBlock block)
    {
        var btn = new Button
        {
            MinWidth = 0, Padding = new Thickness(7),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Content = new SymbolIcon(Symbol.FontColor)
        };
        ToolTipService.SetToolTip(btn, "Color del bloque");
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Padding = new Thickness(8) };
        foreach (var hex in Palette)
        {
            var captured = hex;
            var swatch = new Button
            {
                Width = 26, Height = 26, CornerRadius = new CornerRadius(13),
                Background = Hex(hex), Padding = new Thickness(0), BorderThickness = new Thickness(0)
            };
            swatch.Click += (_, _) => { AppState.Config.SetTaskBlockColor(block.Id, captured); btn.Flyout?.Hide(); Build(); };
            panel.Children.Add(swatch);
        }
        btn.Flyout = new Flyout { Content = panel };
        return btn;
    }

    private FrameworkElement TaskRow(TaskItem task, bool isFirst, bool isLast)
    {
        var grid = new Grid { Padding = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var check = new CheckBox { IsChecked = task.Done, MinWidth = 0, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        check.Click += (_, _) => { AppState.Config.ToggleTask(task.Id); Build(); };
        Grid.SetColumn(check, 0);

        var text = new TextBlock
        {
            Text = task.Text,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Opacity = task.Done ? 0.5 : 1.0
        };
        if (task.Done) text.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
        ToolTipService.SetToolTip(text, "Clic para renombrar");
        text.Tapped += async (_, _) => await RenameTask(task);
        Grid.SetColumn(text, 1);
        grid.Children.Add(check); grid.Children.Add(text);

        if (task.DueDate is { } due)
        {
            var chip = new Border
            {
                Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 1, 6, 1),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0),
                Child = new TextBlock { Text = due.ToString("dd/MM"), FontSize = 11, Opacity = 0.8 }
            };
            Grid.SetColumn(chip, 2);
            grid.Children.Add(chip);
        }

        var up = IconButtonGlyph(ChevronUp, "Subir", () => { AppState.Config.MoveTask(task.Id, true); Build(); }, !isFirst);
        var down = IconButtonGlyph(ChevronDown, "Bajar", () => { AppState.Config.MoveTask(task.Id, false); Build(); }, !isLast);
        var del = IconButton(Symbol.Delete, "Eliminar tarea", () => { AppState.Config.RemoveTask(task.Id); Build(); });
        Grid.SetColumn(up, 3); Grid.SetColumn(down, 4); Grid.SetColumn(del, 5);
        grid.Children.Add(up); grid.Children.Add(down); grid.Children.Add(del);

        return grid;
    }

    // ---------- Diálogos ----------

    private async void AddBlockBtn_Click(object sender, RoutedEventArgs e)
    {
        var box = new TextBox { PlaceholderText = "Nombre del bloque (p. ej. Compras, Trabajo…)" };
        var dlg = new ContentDialog
        {
            Title = "Nuevo bloque", Content = box, PrimaryButtonText = "Crear", CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Primary
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var name = (box.Text ?? "").Trim();
        if (name.Length == 0) return;
        AppState.Config.AddTaskBlock(name);
        Build();
    }

    private async System.Threading.Tasks.Task RenameBlock(TaskBlock block)
    {
        var box = new TextBox { Text = block.Name };
        var dlg = new ContentDialog
        {
            Title = "Renombrar bloque", Content = box, PrimaryButtonText = "Aplicar", CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Primary
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var name = (box.Text ?? "").Trim();
        if (name.Length == 0) return;
        AppState.Config.RenameTaskBlock(block.Id, name);
        Build();
    }

    private async System.Threading.Tasks.Task ConfirmDeleteBlock(TaskBlock block)
    {
        var dlg = new ContentDialog
        {
            Title = "Eliminar bloque",
            Content = $"¿Eliminar «{block.Name}» y todas sus tareas?",
            PrimaryButtonText = "Eliminar", CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Close
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        AppState.Config.RemoveTaskBlock(block.Id);
        Build();
    }

    private async System.Threading.Tasks.Task RenameTask(TaskItem task)
    {
        var box = new TextBox { Text = task.Text, AcceptsReturn = false };
        var dlg = new ContentDialog
        {
            Title = "Editar tarea", Content = box, PrimaryButtonText = "Aplicar", CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Primary
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var text = (box.Text ?? "").Trim();
        if (text.Length == 0) return;
        AppState.Config.RenameTask(task.Id, text);
        Build();
    }
}
