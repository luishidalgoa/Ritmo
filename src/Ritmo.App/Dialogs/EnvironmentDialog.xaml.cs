using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo_App.Services;

namespace Ritmo_App.Dialogs;

/// <summary>Diálogo para crear o editar un entorno de concentración / trabajo (#53/#74).</summary>
public sealed partial class EnvironmentDialog : ContentDialog
{
    private string? _id;   // null = entorno nuevo
    private readonly List<ShortcutLink> _links = [];

    public EnvironmentDialog()
    {
        InitializeComponent();
        // Valores por defecto sensatos para uno nuevo.
        PresetBox.SelectedIndex = 2;   // Profundo
        DndCheck.IsChecked = true;
        BadgesCheck.IsChecked = true;

        // Tooltips de ayuda (#93): el desplegable y cada preset.
        HelpHint.Attach(PresetBox, "pomodoro");
        foreach (var it in PresetBox.Items.OfType<ComboBoxItem>())
            HelpHint.Attach(it, (string)it.Tag switch { "Classic" => "classic", "DeepWork" => "deep-work", _ => "pomodoro" });
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
        CloseBox.Text = string.Join(", ", env.AppsToClose);
        MuteBox.Text = string.Join(", ", env.AppsToMute);
        WebsitesBox.Text = string.Join(", ", env.BlockedWebsites);
        _links.Clear();
        _links.AddRange(env.Links);
        BuildLinksList();
    }

    private void SelectPreset(string? preset)
    {
        var tag = preset ?? "";
        for (int i = 0; i < PresetBox.Items.Count; i++)
            if (PresetBox.Items[i] is ComboBoxItem it && (string)it.Tag == tag)
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
            AppsToClose = SplitCsv(CloseBox.Text),
            AppsToMute = SplitCsv(MuteBox.Text),
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
