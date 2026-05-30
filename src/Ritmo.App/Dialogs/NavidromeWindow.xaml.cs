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

/// <summary>Playlist elegida de Navidrome (la conexión es global). #107</summary>
public sealed record NavidromePick(string PlaylistId, string PlaylistName);

/// <summary>
/// Selector de playlist de Navidrome. Usa la conexión GLOBAL (Ajustes); aquí solo
/// se elige la playlist del entorno. Va como Window. #107
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

    public NavidromeWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(700, 620));
        Closed += (_, _) => { if (!_closed) _tcs.TrySetResult(null); };
    }

    public Task<NavidromePick?> PickAsync()
    {
        Activate();
        _ = LoadAsync();
        return _tcs.Task;
    }

    private void Show(FrameworkElement panel)
    {
        NotConnectedPanel.Visibility = LoadingPanel.Visibility = ErrorPanel.Visibility = ListPanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
    }

    private async Task LoadAsync()
    {
        var settings = AppState.Load();
        if (!NavidromeService.IsConnected(settings)) { Show(NotConnectedPanel); return; }
        ServerSub.Text = settings.NavidromeServerUrl;
        Show(LoadingPanel);
        try
        {
            var playlists = await NavidromeService.GetPlaylistsFromGlobalAsync(settings);
            Populate(playlists);
        }
        catch (Exception ex)
        {
            ErrorText.Text = "No se pudieron cargar las playlists.\n" + ex.Message;
            Show(ErrorPanel);
        }
    }

    private void RetryBtn_Click(object sender, RoutedEventArgs e) => _ = LoadAsync();

    private void Populate(IReadOnlyList<NavidromePlaylist> playlists)
    {
        _all = playlists.Select(p => new PlaylistVm
        {
            Playlist = p,
            Cover = string.IsNullOrEmpty(p.CoverUrl) ? null : new BitmapImage(new Uri(p.CoverUrl))
        }).ToList();

        if (_all.Count == 0)
        {
            ErrorText.Text = "Conectado, pero no hay playlists en el servidor.";
            Show(ErrorPanel);
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
        _tcs.TrySetResult(p is null ? null : new NavidromePick(p.Id, p.Name));
        Close();
    }
}
