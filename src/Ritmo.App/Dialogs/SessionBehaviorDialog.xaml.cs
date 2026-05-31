using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo_App.Services;

namespace Ritmo_App.Dialogs;

/// <summary>
/// Configura, para un TIPO de sesión (por título), qué subconjunto de las apps y
/// enlaces del entorno resuelto se abre al concentrarse. Si no se personaliza, se
/// abre todo. Persiste vía ConfigurationService (entorno ya guardado). #116
/// </summary>
public sealed partial class SessionBehaviorDialog : ContentDialog
{
    private string _envId = "";
    private string _title = "";
    private readonly Dictionary<string, CheckBox> _appChecks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CheckBox> _linkChecks = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasOptions;

    public SessionBehaviorDialog() => InitializeComponent();

    /// <summary>Prepara el diálogo para el título dado (resuelve su entorno por el tipo).</summary>
    public void Configure(string title)
    {
        _title = (title ?? "").Trim();
        TitleText.Text = _title.Length > 0 ? _title : "(sin título)";

        var s = AppState.Load();
        var session = s.Plan.Phases.SelectMany(p => p.Schedule.Sessions).Concat(s.Schedule.Sessions)
            .FirstOrDefault(x => string.Equals(x.Title.Trim(), _title, StringComparison.OrdinalIgnoreCase));
        var categoryId = session?.CategoryId ?? Ritmo.Core.Model.CategoryIds.Other;
        var env = s.ResolveEnvironment(categoryId);

        if (env is null)
        {
            EnvText.Text = "Este tipo de sesión no tiene un entorno asignado.";
            CustomizeSwitch.IsEnabled = false;
            EmptyHint.Text = "Asigna un entorno por defecto o por tipo en Ajustes para poder elegir qué se abre.";
            EmptyHint.Visibility = Visibility.Visible;
            return;
        }

        _envId = env.Id;
        EnvText.Text = $"Entorno: {env.Name}";

        var profile = env.SessionProfiles.FirstOrDefault(
            p => string.Equals(p.SessionTitle.Trim(), _title, StringComparison.OrdinalIgnoreCase));
        bool customized = profile is not null;
        CustomizeSwitch.IsOn = customized;

        var enabledApps = profile?.EnabledApps ?? env.AppsToOpen;
        var enabledLinks = profile?.EnabledLinks ?? env.Links.Select(l => l.Url).ToList();
        BuildLists(env.AppsToOpen, env.Links, enabledApps, enabledLinks);
        UpdateVisibility();
    }

    private void BuildLists(IReadOnlyList<string> openApps, IReadOnlyList<ShortcutLink> links,
                            IReadOnlyList<string> enabledApps, IReadOnlyList<string> enabledLinks)
    {
        ListsPanel.Children.Clear();
        _appChecks.Clear(); _linkChecks.Clear();
        _hasOptions = openApps.Count > 0 || links.Count > 0;

        if (openApps.Count > 0)
        {
            ListsPanel.Children.Add(new TextBlock { Text = "Apps a abrir", FontSize = 12, Opacity = 0.7 });
            foreach (var p in openApps)
            {
                var cb = new CheckBox { Content = KnownApps.ByProcess(p)?.Name ?? p, IsChecked = enabledApps.Contains(p, StringComparer.OrdinalIgnoreCase) };
                _appChecks[p] = cb;
                ListsPanel.Children.Add(cb);
            }
        }
        if (links.Count > 0)
        {
            ListsPanel.Children.Add(new TextBlock { Text = "Páginas", FontSize = 12, Opacity = 0.7 });
            foreach (var l in links)
            {
                var cb = new CheckBox { Content = l.Title, IsChecked = enabledLinks.Contains(l.Url, StringComparer.OrdinalIgnoreCase) };
                _linkChecks[l.Url] = cb;
                ListsPanel.Children.Add(cb);
            }
        }
        if (!_hasOptions)
        {
            EmptyHint.Text = "Este entorno no tiene apps ni enlaces que abrir.";
            EmptyHint.Visibility = Visibility.Visible;
            CustomizeSwitch.IsEnabled = false;
        }
    }

    private void CustomizeSwitch_Toggled(object sender, RoutedEventArgs e) => UpdateVisibility();

    private void UpdateVisibility()
        => ListsPanel.Visibility = (_hasOptions && CustomizeSwitch.IsOn) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Persiste la elección. Llamar solo si el resultado fue Primary (Guardar).</summary>
    public void Apply()
    {
        if (_envId.Length == 0 || !_hasOptions) return;
        if (CustomizeSwitch.IsOn)
        {
            var links = _linkChecks.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
            var apps = _appChecks.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
            AppState.Config.SetSessionProfile(_envId, _title, links, apps);
        }
        else AppState.Config.ClearSessionProfile(_envId, _title);
    }
}
