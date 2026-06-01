using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;
using Ritmo_App.Services;

namespace Ritmo_App;

/// <summary>
/// Página «Tareas» (#145): listas de tareas estilo Recordatorios de Apple, en master-detail —
/// a la izquierda la lista de bloques, a la derecha la VISTA COMPLETA del bloque elegido (no un
/// dropdown). Un bloque puede ir suelto o vincularse a un entorno: en ese caso sus tareas se
/// organizan en "cajones" por cada sesión del entorno (fase 3). La sincronización externa queda
/// para una fase posterior.
/// </summary>
public sealed partial class TasksPage : Page
{
    private static readonly string[] Palette =
        { "#E53935", "#FB8C00", "#FDD835", "#43A047", "#1E88E5", "#8E24AA", "#6D4C41", "#546E7A" };
    // Chevrons de Segoe Fluent (el enum Symbol no tiene «Down»); se construyen por code point.
    private static readonly string ChevronUp = ((char)0xE70E).ToString();
    private static readonly string ChevronDown = ((char)0xE70D).ToString();

    private string? _selectedBlockId;

    /// <summary>Bloque a seleccionar al abrir la página (lo fija Navigator.GoToTasks desde un entorno).</summary>
    internal static string? PendingBlockId;

    public TasksPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (PendingBlockId is not null) { _selectedBlockId = PendingBlockId; PendingBlockId = null; }
            Build();
        };
    }

    /// <summary>Permite abrir la página con un bloque concreto seleccionado (p. ej. desde un entorno).</summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string id && !string.IsNullOrWhiteSpace(id)) _selectedBlockId = id;
    }

    private void Build()
    {
        var s = AppState.Load();
        var blocks = s.TaskBlocks.OrderBy(b => b.Order).ToList();
        if (_selectedBlockId is null || blocks.All(b => b.Id != _selectedBlockId))
            _selectedBlockId = blocks.FirstOrDefault()?.Id;

        BuildBlockList(s, blocks);
        BuildDetail(s, blocks.FirstOrDefault(b => b.Id == _selectedBlockId));
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
            BorderThickness = new Thickness(0), IsEnabled = enabled
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
            BorderThickness = new Thickness(0), IsEnabled = enabled
        };
        ToolTipService.SetToolTip(b, tooltip);
        b.Click += (_, _) => onClick();
        return b;
    }

    // ---------- Columna izquierda: lista de bloques ----------

    private void BuildBlockList(AppSettings s, List<TaskBlock> blocks)
    {
        BlockListHost.Children.Clear();
        foreach (var b in blocks)
        {
            int pending = s.Tasks.Count(t => t.BlockId == b.Id && !t.Done);
            bool selected = b.Id == _selectedBlockId;

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var dot = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = Hex(b.ColorHex), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            Grid.SetColumn(dot, 0);
            var name = new TextBlock { Text = b.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal };
            Grid.SetColumn(name, 1);
            var count = new TextBlock { Text = pending == 0 ? "" : pending.ToString(), Opacity = 0.55, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(count, 2);
            row.Children.Add(dot); row.Children.Add(name); row.Children.Add(count);

            var item = new Button
            {
                Content = row,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8, 10, 8),
                BorderThickness = new Thickness(0),
                Background = selected
                    ? (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            var captured = b.Id;
            item.Click += (_, _) => { _selectedBlockId = captured; Build(); };
            BlockListHost.Children.Add(item);
        }
    }

    // ---------- Columna derecha: vista completa del bloque ----------

    private void BuildDetail(AppSettings s, TaskBlock? block)
    {
        DetailHost.Children.Clear();
        if (block is null)
        {
            DetailHost.Children.Add(new TextBlock
            {
                Text = "Selecciona un bloque a la izquierda o crea uno nuevo para empezar.",
                Opacity = 0.6, FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        // --- Cabecera del bloque ---
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var dot = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Background = Hex(block.ColorHex), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        Grid.SetColumn(dot, 0);
        var title = new TextBlock { Text = block.Name, FontSize = 26, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(title, 1);
        var acts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        acts.Children.Add(ColorButton(block));
        acts.Children.Add(IconButton(Symbol.Rename, "Renombrar bloque", async () => await RenameBlock(block)));
        acts.Children.Add(IconButton(Symbol.Delete, "Eliminar bloque", async () => await ConfirmDeleteBlock(block)));
        Grid.SetColumn(acts, 2);
        header.Children.Add(dot); header.Children.Add(title); header.Children.Add(acts);
        DetailHost.Children.Add(header);

        // --- Vínculo a entorno ---
        DetailHost.Children.Add(EnvironmentPicker(s, block));

        // --- Tareas: cajones por sesión (si está vinculado) o lista plana ---
        if (string.IsNullOrEmpty(block.EnvironmentId))
        {
            var tasks = s.Tasks.Where(t => t.BlockId == block.Id).OrderBy(t => t.Order).ToList();
            DetailHost.Children.Add(TaskListView(tasks));
            DetailHost.Children.Add(AddTaskRow(block.Id, null));
        }
        else
        {
            BuildSessionDrawers(s, block);
        }
    }

    private FrameworkElement EnvironmentPicker(AppSettings s, TaskBlock block)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 0, 0, 4) };
        row.Children.Add(new TextBlock { Text = "Entorno:", Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
        var combo = new ComboBox { MinWidth = 220 };
        combo.Items.Add(new ComboBoxItem { Content = "(Sin vincular)", Tag = "" });
        foreach (var env in s.FocusEnvironments)
            combo.Items.Add(new ComboBoxItem { Content = env.Name, Tag = env.Id });
        combo.SelectedIndex = 0;
        for (int i = 0; i < combo.Items.Count; i++)
            if (combo.Items[i] is ComboBoxItem it && (string)it.Tag == (block.EnvironmentId ?? "")) { combo.SelectedIndex = i; break; }
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is not ComboBoxItem it) return;
            var envId = (string)it.Tag;
            if ((block.EnvironmentId ?? "") == envId) return;
            AppState.Config.SetTaskBlockEnvironment(block.Id, string.IsNullOrEmpty(envId) ? null : envId);
            Build();
        };
        row.Children.Add(combo);
        if (!string.IsNullOrEmpty(block.EnvironmentId))
            row.Children.Add(new TextBlock { Text = "· las tareas se agrupan por sesión", Opacity = 0.55, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    /// <summary>Sesiones recurrentes DISTINTAS que usan un entorno (su categoría lo resuelve). #145</summary>
    private static List<StudySession> EnvironmentSessions(AppSettings s, string envId)
    {
        var all = s.Plan.Phases.SelectMany(p => p.Schedule.Sessions).Concat(s.Schedule.Sessions);
        var seen = new HashSet<string>();
        var result = new List<StudySession>();
        foreach (var x in all)
        {
            if (s.ResolveEnvironment(x.CategoryId)?.Id != envId) continue;
            if (seen.Add(SessionKey.For(x))) result.Add(x);
        }
        return result.OrderBy(x => x.Start).ToList();
    }

    private void BuildSessionDrawers(AppSettings s, TaskBlock block)
    {
        var sessions = EnvironmentSessions(s, block.EnvironmentId!);
        var blockTasks = s.Tasks.Where(t => t.BlockId == block.Id).ToList();
        var usedKeys = new HashSet<string>();

        if (sessions.Count == 0)
            DetailHost.Children.Add(new TextBlock { Text = "Este entorno aún no tiene sesiones en el horario. Añádelas y aparecerán aquí como cajones.", Opacity = 0.6, FontSize = 13, TextWrapping = TextWrapping.Wrap });

        foreach (var ses in sessions)
        {
            var key = SessionKey.For(ses);
            usedKeys.Add(key);
            var label = $"{ses.Title}  ·  {ses.Start:HH\\:mm}";
            var tasks = blockTasks.Where(t => t.SessionKey == key).OrderBy(t => t.Order).ToList();
            DetailHost.Children.Add(DrawerSection(label, Hex(block.ColorHex), TaskListView(tasks), AddTaskRow(block.Id, key)));
        }

        // Cajón "General": tareas del bloque sin sesión (o cuya sesión ya no existe).
        var orphan = blockTasks.Where(t => string.IsNullOrEmpty(t.SessionKey) || !usedKeys.Contains(t.SessionKey!))
                               .OrderBy(t => t.Order).ToList();
        DetailHost.Children.Add(DrawerSection("General (sin sesión)", Hex(block.ColorHex, 0.5), TaskListView(orphan), AddTaskRow(block.Id, null)));
    }

    private FrameworkElement DrawerSection(string label, Brush accent, FrameworkElement list, FrameworkElement addRow)
    {
        var card = new Border
        {
            CornerRadius = new CornerRadius(10), Padding = new Thickness(16),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], BorderThickness = new Thickness(1)
        };
        var st = new StackPanel { Spacing = 8 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        head.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = accent, VerticalAlignment = VerticalAlignment.Center });
        head.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        st.Children.Add(head);
        st.Children.Add(list);
        st.Children.Add(addRow);
        card.Child = st;
        return card;
    }

    private FrameworkElement TaskListView(List<TaskItem> tasks)
    {
        if (tasks.Count == 0)
            return new TextBlock { Text = "Sin tareas.", Opacity = 0.5, FontSize = 13 };
        var list = new StackPanel { Spacing = 2 };
        for (int i = 0; i < tasks.Count; i++)
            list.Children.Add(TaskRow(tasks[i], i == 0, i == tasks.Count - 1));
        return list;
    }

    private FrameworkElement AddTaskRow(string blockId, string? sessionKey)
    {
        var addRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var input = new TextBox { PlaceholderText = "Nueva tarea…", Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(input, 0);
        void Add()
        {
            var text = (input.Text ?? "").Trim();
            if (text.Length == 0) return;
            AppState.Config.AddTask(blockId, text, sessionKey);
            Build();
        }
        input.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) Add(); };
        var btn = new Button { Content = "Añadir" };
        btn.Click += (_, _) => Add();
        Grid.SetColumn(btn, 1);
        addRow.Children.Add(input); addRow.Children.Add(btn);
        return addRow;
    }

    private Button ColorButton(TaskBlock block)
    {
        var btn = new Button
        {
            MinWidth = 0, Padding = new Thickness(7),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0), Content = new SymbolIcon(Symbol.FontColor)
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

        var check = new CheckBox { IsChecked = task.Done, MinWidth = 0, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        check.Click += (_, _) => { AppState.Config.ToggleTask(task.Id); Build(); };
        Grid.SetColumn(check, 0);

        var text = new TextBlock
        {
            Text = task.Text, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
            Opacity = task.Done ? 0.5 : 1.0
        };
        if (task.Done) text.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
        ToolTipService.SetToolTip(text, "Clic para renombrar");
        text.Tapped += async (_, _) => await RenameTask(task);
        Grid.SetColumn(text, 1);
        grid.Children.Add(check); grid.Children.Add(text);

        var up = IconButtonGlyph(ChevronUp, "Subir", () => { AppState.Config.MoveTask(task.Id, true); Build(); }, !isFirst);
        var down = IconButtonGlyph(ChevronDown, "Bajar", () => { AppState.Config.MoveTask(task.Id, false); Build(); }, !isLast);
        var del = IconButton(Symbol.Delete, "Eliminar tarea", () => { AppState.Config.RemoveTask(task.Id); Build(); });
        Grid.SetColumn(up, 2); Grid.SetColumn(down, 3); Grid.SetColumn(del, 4);
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
        var r = AppState.Config.AddTaskBlock(name);
        if (r.Success) _selectedBlockId = r.Message;
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
            Title = "Eliminar bloque", Content = $"¿Eliminar «{block.Name}» y todas sus tareas?",
            PrimaryButtonText = "Eliminar", CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Close
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        AppState.Config.RemoveTaskBlock(block.Id);
        _selectedBlockId = null;
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
