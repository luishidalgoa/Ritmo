using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Focus;

namespace Ritmo_App.Dialogs;

/// <summary>Diálogo para crear o editar un entorno de concentración (#53).</summary>
public sealed partial class EnvironmentDialog : ContentDialog
{
    private string? _id;   // null = entorno nuevo

    public EnvironmentDialog()
    {
        InitializeComponent();
        // Valores por defecto sensatos para uno nuevo.
        PresetBox.SelectedIndex = 2;   // Profundo
        DndCheck.IsChecked = true;
        BadgesCheck.IsChecked = true;
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
            Music = music
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
