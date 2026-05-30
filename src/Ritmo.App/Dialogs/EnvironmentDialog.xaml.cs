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
    private readonly List<string> _blockedWebsites = [];   // #99
    private readonly List<string> _otherClose = [];        // apps fuera del catálogo (#100)
    private string? _navPlaylistId, _navPlaylistName;   // Navidrome: solo playlist (conexión global) (#107)

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
        BuildMusicSelector();   // #98
        BuildWebsList();        // #99
        BuildOtherApps();       // #100
    }

    // ---------- Música: Navidrome (servidor propio). Spotify desactivado (#106/#107) ----------

    private void BuildMusicSelector()
    {
        MusicAppBox.Items.Clear();
        MusicAppBox.Items.Add(new ComboBoxItem { Content = "Ninguna", Tag = "" });
        MusicAppBox.Items.Add(new ComboBoxItem { Content = "Navidrome", Tag = "navidrome" });
        MusicAppBox.SelectedIndex = 0;
    }

    private void MusicAppBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var tag = (MusicAppBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        if (NavidromePanel is not null)
        {
            NavidromePanel.Visibility = tag == "navidrome" ? Visibility.Visible : Visibility.Collapsed;
            if (tag == "navidrome") UpdateNavidromeLabel();
        }
    }

    private void SelectMusic(MusicLauncher? music)
    {
        if (music is { Provider: "navidrome" })
        {
            SelectByTag("navidrome");
            _navPlaylistId = music.PlaylistId;
            _navPlaylistName = music.PlaylistName;
            UpdateNavidromeLabel();
        }
        else MusicAppBox.SelectedIndex = 0;   // Spotify desactivado / música antigua → Ninguna
    }

    private void SelectByTag(string tag)
    {
        for (int i = 0; i < MusicAppBox.Items.Count; i++)
            if (MusicAppBox.Items[i] is ComboBoxItem it && (string)it.Tag == tag) { MusicAppBox.SelectedIndex = i; return; }
        MusicAppBox.SelectedIndex = 0;
    }

    private void UpdateNavidromeLabel()
    {
        bool connected = Services.NavidromeService.IsConnected(Services.AppState.Load());
        ConfigureNavidromeBtn.IsEnabled = connected;   // sin conexión global → botón deshabilitado
        if (!connected)
        {
            NavidromeLabel.Text = "Conecta Navidrome en Ajustes → Apps vinculadas.";
            return;
        }
        NavidromeLabel.Text = _navPlaylistName is { Length: > 0 }
            ? $"Playlist: {_navPlaylistName}"
            : "Sin playlist elegida";
    }

    private async void ConfigureNavidromeBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var win = new NavidromeWindow();
            var pick = await win.PickAsync();
            if (pick is not null)
            {
                _navPlaylistId = pick.PlaylistId;
                _navPlaylistName = pick.PlaylistName;
                UpdateNavidromeLabel();
            }
        }
        catch (Exception ex)
        {
            NavidromeLabel.Text = "Error: " + ex.Message;
        }
    }

    // ---------- Webs a bloquear: input + lista con favicon + quitar (#99) ----------

    private void AddWebBtn_Click(object sender, RoutedEventArgs e) => AddWeb(WebInputBox.Text);

    private void WebInputBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) { AddWeb(WebInputBox.Text); e.Handled = true; }
    }

    private void AddWeb(string raw)
    {
        var domain = WebDomain.Normalize(raw);
        if (domain.Length == 0) return;
        if (!_blockedWebsites.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase)))
            _blockedWebsites.Add(domain);
        WebInputBox.Text = "";
        BuildWebsList();
    }

    private void BuildWebsList()
    {
        WebsList.Children.Clear();
        foreach (var d in _blockedWebsites) WebsList.Children.Add(WebRow(d));
    }

    private FrameworkElement WebRow(string domain)
    {
        // Favicon con globo de respaldo si falla la carga.
        var fav = new Image { Width = 16, Height = 16, Visibility = Visibility.Collapsed };
        var globe = new SymbolIcon(Symbol.World) { Opacity = 0.6 };
        try
        {
            var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                new Uri($"https://www.google.com/s2/favicons?domain={domain}&sz=32"));
            bmp.ImageOpened += (_, _) => { fav.Visibility = Visibility.Visible; globe.Visibility = Visibility.Collapsed; };
            bmp.ImageFailed += (_, _) => { fav.Visibility = Visibility.Collapsed; globe.Visibility = Visibility.Visible; };
            fav.Source = bmp;
        }
        catch { }
        var iconHost = new Grid { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        iconHost.Children.Add(globe); iconHost.Children.Add(fav);
        Grid.SetColumn(iconHost, 0);

        var text = new TextBlock { Text = domain, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(text, 1);

        var x = MakeRemoveButton("Quitar de la lista", () => { _blockedWebsites.Remove(domain); BuildWebsList(); });
        Grid.SetColumn(x, 2);

        var grid = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(iconHost); grid.Children.Add(text); grid.Children.Add(x);
        return grid;
    }

    /// <summary>Botón de quitar: fondo transparente, rojo al pasar el ratón (#99).</summary>
    private static Button MakeRemoveButton(string tooltip, Action onClick)
    {
        var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var hoverRed = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(60, 232, 17, 35));
        var btn = new Button
        {
            Content = new SymbolIcon(Symbol.Cancel),
            Background = transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(btn, tooltip);
        btn.PointerEntered += (_, _) => btn.Background = hoverRed;
        btn.PointerExited += (_, _) => btn.Background = transparent;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    // ---------- Otras apps fuera del catálogo: chips quitables, sin escribir texto (#100) ----------

    private void BuildOtherApps()
    {
        OtherAppsPanel.Children.Clear();
        if (_otherClose.Count == 0) return;
        OtherAppsPanel.Children.Add(new TextBlock { Text = "Otras apps a cerrar", Opacity = 0.6, FontSize = 12 });
        foreach (var p in _otherClose.ToList())
        {
            var name = new TextBlock { Text = p, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(name, 0);
            var local = p;
            var x = MakeRemoveButton("Quitar", () => { _otherClose.Remove(local); BuildOtherApps(); });
            Grid.SetColumn(x, 1);
            var g = new Grid { ColumnSpacing = 6 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(name); g.Children.Add(x);
            OtherAppsPanel.Children.Add(g);
        }
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

        var combo = new ComboBox { MinWidth = 120, VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center };
        combo.Items.Add(new ComboBoxItem { Content = "No tocar", Tag = "" });
        combo.Items.Add(new ComboBoxItem { Content = "Abrir", Tag = "open" });
        combo.Items.Add(new ComboBoxItem { Content = "Cerrar", Tag = "close" });
        combo.Items.Add(new ComboBoxItem { Content = "Silenciar", Tag = "mute" });
        var current = _appActions.TryGetValue(app.ProcessName, out var a) ? a : "";
        combo.SelectedIndex = current switch { "open" => 1, "close" => 2, "mute" => 3, _ => 0 };
        combo.SelectionChanged += (_, _) =>
        {
            var tag = (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            if (tag is "open" or "close" or "mute") _appActions[app.ProcessName] = tag;
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
        OpenLinksCheck.IsChecked = env.OpenLinksInBrowser;
        NewDesktopCheck.IsChecked = env.NewVirtualDesktop;   // #110
        AutoPlayCheck.IsChecked = env.Music?.AutoPlay ?? false;
        SelectMusic(env.Music);   // #98

        // Webs (#99): normalizadas a dominio.
        _blockedWebsites.Clear();
        _blockedWebsites.AddRange(env.BlockedWebsites.Select(WebDomain.Normalize).Where(d => d.Length > 0).Distinct());
        BuildWebsList();

        // Apps: catálogo → acción; las que no están en el catálogo → chips quitables (#100).
        _appActions.Clear();
        _otherClose.Clear();
        foreach (var p in env.AppsToClose)
            if (KnownApps.ByProcess(p) is not null) _appActions[p] = "close";
            else if (!_otherClose.Contains(p)) _otherClose.Add(p);
        foreach (var p in env.AppsToMute)
            if (KnownApps.ByProcess(p) is not null) _appActions[p] = "mute";
        foreach (var p in env.AppsToOpen)
            if (KnownApps.ByProcess(p) is not null) _appActions[p] = "open";   // #109
        BuildAppsSelector();
        BuildOtherApps();

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

        return new FocusEnvironment
        {
            Id = _id ?? $"env-{Guid.NewGuid():N}"[..12],
            Name = name,
            PomodoroPreset = preset,
            EnableDoNotDisturb = DndCheck.IsChecked == true,
            HideTaskbarBadges = BadgesCheck.IsChecked == true,
            OpenLinksInBrowser = OpenLinksCheck.IsChecked == true,
            NewVirtualDesktop = NewDesktopCheck.IsChecked == true,
            ShowDayPreview = true,
            AppsToClose = [.. _appActions.Where(kv => kv.Value == "close").Select(kv => kv.Key), .. _otherClose],
            AppsToMute = _appActions.Where(kv => kv.Value == "mute").Select(kv => kv.Key).Distinct().ToList(),
            AppsToOpen = _appActions.Where(kv => kv.Value == "open").Select(kv => kv.Key).Distinct().ToList(),
            BlockedWebsites = _blockedWebsites.ToList(),
            Music = BuildMusic(),
            Links = _links.ToList()
        };
    }

    /// <summary>Construye el MusicLauncher. Solo Navidrome; el servidor es global (#107).</summary>
    private MusicLauncher? BuildMusic()
    {
        var tag = (MusicAppBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        if (tag != "navidrome" || string.IsNullOrEmpty(_navPlaylistId)) return null;
        var server = Services.AppState.Load().NavidromeServerUrl;
        if (string.IsNullOrEmpty(server)) return null;

        return new MusicLauncher
        {
            Name = "Navidrome",
            Provider = "navidrome",
            PlaylistId = _navPlaylistId,
            PlaylistName = _navPlaylistName,
            Target = Services.NavidromeService.PlaylistWebUrl(server, _navPlaylistId),
            AutoPlay = AutoPlayCheck.IsChecked == true
        };
    }
}
