using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Ritmo_App.Services;

namespace Ritmo_App.Dialogs;

/// <summary>Resultado de configurar Navidrome (la contraseña no se devuelve/persiste). #107</summary>
public sealed record NavidromePick(string ServerUrl, string User, string PlaylistId, string PlaylistName);

/// <summary>
/// Ventana de configuración de Navidrome: servidor + credenciales → listar y elegir
/// playlist. Va como Window (se abre desde el diálogo de entorno). #107
/// </summary>
public sealed partial class NavidromeWindow : Window
{
    public sealed class PlaylistVm
    {
        public required NavidromePlaylist Playlist { get; init; }
        public ImageSource? Cover { get; init; }
        public string Name => Playlist.Name;
        public string Meta => $"{Playlist.SongCount} canciones" + (string.IsNullOrEmpty(Playlist.Owner) ? "" : $" · {Playlist.Owner}");
    }

    private readonly TaskCompletionSource<NavidromePick?> _tcs = new();
    private List<PlaylistVm> _all = [];
    private bool _closed;
    private string _server = "";
    private string _user = "";

    public NavidromeWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(700, 660));
        Closed += (_, _) => { if (!_closed) _tcs.TrySetResult(null); };
    }

    /// <summary>Pre-rellena servidor/usuario al editar un entorno ya configurado.</summary>
    public void Prefill(string? server, string? user)
    {
        if (!string.IsNullOrEmpty(server)) ServerBox.Text = server;
        if (!string.IsNullOrEmpty(user)) UserBox.Text = user;
    }

    public Task<NavidromePick?> PickAsync()
    {
        Activate();
        return _tcs.Task;
    }

    private void Show(FrameworkElement panel)
    {
        ConfigPanel.Visibility = LoadingPanel.Visibility = ListPanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
    }

    private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
    {
        _server = ServerBox.Text.Trim();
        _user = UserBox.Text.Trim();
        var pass = PassBox.Password;
        ConfigError.Visibility = Visibility.Collapsed;
        if (_server.Length == 0 || _user.Length == 0 || pass.Length == 0)
        {
            ConfigError.Text = "Rellena servidor, usuario y contraseña.";
            ConfigError.Visibility = Visibility.Visible;
            return;
        }

        Show(LoadingPanel);
        try
        {
            var playlists = await NavidromeService.GetPlaylistsAsync(_server, _user, pass);
            Populate(playlists);
        }
        catch (Exception ex)
        {
            ConfigError.Text = "No se pudo conectar: " + ex.Message;
            Show(ConfigPanel);
            ConfigError.Visibility = Visibility.Visible;
        }
    }

    private void Populate(IReadOnlyList<NavidromePlaylist> playlists)
    {
        _all = playlists.Select(p => new PlaylistVm
        {
            Playlist = p,
            Cover = string.IsNullOrEmpty(p.CoverUrl) ? null : new BitmapImage(new Uri(p.CoverUrl))
        }).ToList();

        if (_all.Count == 0)
        {
            ConfigError.Text = "Conectado, pero no hay playlists en el servidor.";
            Show(ConfigPanel);
            ConfigError.Visibility = Visibility.Visible;
            return;
        }
        ApplyFilter(SearchBox.Text);
        Show(ListPanel);
    }

    private void ApplyFilter(string? query)
    {
        var q = (query ?? "").Trim();
        IEnumerable<PlaylistVm> items = _all;
        if (q.Length > 0) items = _all.Where(v => v.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        PlaylistGrid.ItemsSource = items.ToList();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter(SearchBox.Text);

    private void PlaylistGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UseBtn.IsEnabled = PlaylistGrid.SelectedItem is not null;

    private void PlaylistGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PlaylistVm vm) Finish(vm.Playlist);
    }

    private void UseBtn_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistGrid.SelectedItem is PlaylistVm vm) Finish(vm.Playlist);
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e) => Finish(null);

    private void Finish(NavidromePlaylist? p)
    {
        _closed = true;
        _tcs.TrySetResult(p is null ? null : new NavidromePick(_server, _user, p.Id, p.Name));
        Close();
    }
}
