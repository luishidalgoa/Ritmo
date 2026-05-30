using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo.Core.Pomodoro;
using Ritmo_App.Services;

namespace Ritmo_App.Dialogs;

/// <summary>Diálogo para crear o editar un entorno de concentración / trabajo (#53/#74).</summary>
public sealed partial class EnvironmentDialog : ContentDialog
{
    private string? _id;   // null = entorno nuevo
    private readonly List<ShortcutLink> _links = [];
    // Acción por app del catálogo: processName → "close" | "mute" (#94).
    private readonly Dictionary<string, string> _appActions = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _installed = new(StringComparer.OrdinalIgnoreCase);

    public EnvironmentDialog()
    {
        InitializeComponent();
        BuildPresetList();
        // Valores por defecto sensatos para uno nuevo: "Profundo".
        SelectPreset(PomodoroRhythms.DeepWorkId);
        DndCheck.IsChecked = true;
        BadgesCheck.IsChecked = true;

        // Catálogo de apps por categoría + detección de instaladas (#94).
        _installed = Services.InstalledApps.DetectInstalled();
        BuildAppsSelector();
    }

    /// <summary>Llena el desplegable de ritmos: por defecto de la app + de por defecto + propios (#96).</summary>
    private void BuildPresetList()
    {
        PresetBox.Items.Clear();
        var dflt = new ComboBoxItem { Content = "Por defecto de la app", Tag = "" };
        HelpHint.Attach(dflt, "pomodoro");
        PresetBox.Items.Add(dflt);

        foreach (var r in PomodoroRhythms.All(Services.AppState.Load().Rhythms))
        {
            var item = new ComboBoxItem
            {
                Content = $"{r.Name} ({r.FocusMinutes}/{r.ShortBreakMinutes}/{r.LongBreakMinutes})",
                Tag = r.Id
            };
            HelpHint.Attach(item, r.Id switch
            {
                PomodoroRhythms.ClassicId => "classic",
                PomodoroRhythms.DeepWorkId => "deep-work",
                _ => "pomodoro"
            });
            PresetBox.Items.Add(item);
        }
        HelpHint.Attach(PresetBox, "pomodoro");
    }

    private void BuildAppsSelector()
    {
        AppsPanel.Children.Clear();
        foreach (var (cat, apps) in KnownApps.ByCategory())
        {
            var list = new StackPanel { Spacing = 2 };
            foreach (var app in apps) list.Children.Add(AppRow(app));
            AppsPanel.Children.Add(new Expander
            {
                Header = KnownApps.Label(cat),
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                IsExpanded = false,
                Content = list
            });
        }
    }

    private Microsoft.UI.Xaml.FrameworkElement AppRow(KnownApp app)
    {
        bool installed = _installed.Contains(app.ProcessName);

        var name = new TextBlock
        {
            Text = app.Name + (installed ? "" : "  (no instalada)"),
            Opacity = installed ? 1.0 : 0.6,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center
        };
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(name, 0);

        var combo = new ComboBox { MinWidth = 110, VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center };
        combo.Items.Add(new ComboBoxItem { Content = "No tocar", Tag = "" });
        combo.Items.Add(new ComboBoxItem { Content = "Cerrar", Tag = "close" });
        combo.Items.Add(new ComboBoxItem { Content = "Silenciar", Tag = "mute" });
        var current = _appActions.TryGetValue(app.ProcessName, out var a) ? a : "";
        combo.SelectedIndex = current switch { "close" => 1, "mute" => 2, _ => 0 };
        combo.SelectionChanged += (_, _) =>
        {
            var tag = (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            if (tag is "close" or "mute") _appActions[app.ProcessName] = tag;
            else _appActions.Remove(app.ProcessName);
        };
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(combo, 1);

        var grid = new Grid { ColumnSpacing = 6, Margin = new Microsoft.UI.Xaml.Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new Microsoft.UI.Xaml.GridLength(1, Microsoft.UI.Xaml.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = Microsoft.UI.Xaml.GridLength.Auto });
        grid.Children.Add(name); grid.Children.Add(combo);

        if (!installed)
        {
            var install = new HyperlinkButton { Content = "Instalar", Padding = new Microsoft.UI.Xaml.Thickness(6, 0, 0, 0) };
            var url = app.InstallUrl;
            install.Click += (_, _) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { } };
            Microsoft.UI.Xaml.Controls.Grid.SetColumn(install, 2);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = Microsoft.UI.Xaml.GridLength.Auto });
            grid.Children.Add(install);
        }
        return grid;
    }

    private void BuildLinksList()
    {
        LinksList.Children.Clear();
        for (int i = 0; i < _links.Count; i++)
        {
            var link = _links[i];
            int index = i;
            var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            text.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = link.Title + "  ", FontWeight = FontWeights.SemiBold });
            text.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = link.Url, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
            Grid.SetColumn(text, 0);

            var del = new Button { Content = new SymbolIcon(Symbol.Delete) };
            del.Click += (_, _) => { _links.RemoveAt(index); BuildLinksList(); };
            Grid.SetColumn(del, 1);

            var g = new Grid { ColumnSpacing = 6 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(text); g.Children.Add(del);
            LinksList.Children.Add(g);
        }
    }

    private void AddLinkBtn_Click(object sender, RoutedEventArgs e)
    {
        var title = LinkTitleBox.Text.Trim();
        var url = LinkUrlBox.Text.Trim();
        if (title.Length == 0 || url.Length == 0) return;
        _links.Add(new ShortcutLink { Title = title, Url = url });
        LinkTitleBox.Text = ""; LinkUrlBox.Text = "";
        BuildLinksList();
    }

    /// <summary>Carga un entorno existente para editarlo.</summary>
    public void LoadFrom(FocusEnvironment env)
    {
        _id = env.Id;
        NameBox.Text = env.Name;
        SelectPreset(env.PomodoroPreset);
        DndCheck.IsChecked = env.EnableDoNotDisturb;
        BadgesCheck.IsChecked = env.HideTaskbarBadges;
        EdgeCheck.IsChecked = env.OpenStudyListInEdge;
        MusicNameBox.Text = env.Music?.Name ?? "";
        MusicTargetBox.Text = env.Music?.Target ?? "";
        AutoPlayCheck.IsChecked = env.Music?.AutoPlay ?? false;
        WebsitesBox.Text = string.Join(", ", env.BlockedWebsites);

        // Apps: catálogo → acción; las que no están en el catálogo → "Otras a cerrar".
        _appActions.Clear();
        var others = new List<string>();
        foreach (var p in env.AppsToClose)
            if (KnownApps.ByProcess(p) is not null) _appActions[p] = "close"; else others.Add(p);
        foreach (var p in env.AppsToMute)
            if (KnownApps.ByProcess(p) is not null) _appActions[p] = "mute"; else others.Add(p);
        OtherCloseBox.Text = string.Join(", ", others.Distinct());
        BuildAppsSelector();

        _links.Clear();
        _links.AddRange(env.Links);
        BuildLinksList();
    }

    private void SelectPreset(string? preset)
    {
        // Tolera ids nuevos y nombres heredados ("Classic"/"DeepWork").
        var tag = PomodoroRhythms.Find(preset, Services.AppState.Load().Rhythms)?.Id ?? "";
        for (int i = 0; i < PresetBox.Items.Count; i++)
            if (PresetBox.Items[i] is ComboBoxItem it && string.Equals((string)it.Tag, tag, StringComparison.OrdinalIgnoreCase))
            { PresetBox.SelectedIndex = i; return; }
        PresetBox.SelectedIndex = 0;
    }

    /// <summary>Construye el entorno. Genera un Id si es nuevo.</summary>
    public FocusEnvironment ToEnvironment()
    {
        var name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Entorno" : NameBox.Text.Trim();
        var preset = (PresetBox.SelectedItem as ComboBoxItem)?.Tag as string;
        if (string.IsNullOrEmpty(preset)) preset = null;

        MusicLauncher? music = null;
        var target = MusicTargetBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(target))
            music = new MusicLauncher
            {
                Name = string.IsNullOrWhiteSpace(MusicNameBox.Text) ? "Música" : MusicNameBox.Text.Trim(),
                Target = target,
                AutoPlay = AutoPlayCheck.IsChecked == true
            };

        return new FocusEnvironment
        {
            Id = _id ?? $"env-{Guid.NewGuid():N}"[..12],
            Name = name,
            PomodoroPreset = preset,
            EnableDoNotDisturb = DndCheck.IsChecked == true,
            HideTaskbarBadges = BadgesCheck.IsChecked == true,
            OpenStudyListInEdge = EdgeCheck.IsChecked == true,
            ShowDayPreview = true,
            AppsToClose = [.. _appActions.Where(kv => kv.Value == "close").Select(kv => kv.Key), .. SplitCsv(OtherCloseBox.Text)],
            AppsToMute = _appActions.Where(kv => kv.Value == "mute").Select(kv => kv.Key).Distinct().ToList(),
            BlockedWebsites = SplitCsv(WebsitesBox.Text),
            Music = music,
            Links = _links.ToList()
        };
    }

    /// <summary>Separa una lista por comas, recortando espacios y descartando vacíos.</summary>
    private static IReadOnlyList<string> SplitCsv(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Distinct()
                 .ToList();
}
