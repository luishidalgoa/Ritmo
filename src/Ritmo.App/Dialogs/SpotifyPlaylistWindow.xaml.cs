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

/// <summary>
/// Ventana modal con estética Spotify para elegir una playlist del usuario (#106).
/// Va como Window (no ContentDialog) porque se abre desde el diálogo de entorno y
/// WinUI no permite dos ContentDialog a la vez. Resultado vía <see cref="PickAsync"/>.
/// </summary>
public sealed partial class SpotifyPlaylistWindow : Window
{
    /// <summary>Item para pintar (record envuelto con su carátula ya resuelta).</summary>
    public sealed class PlaylistVm
    {
        public required SpotifyPlaylist Playlist { get; init; }
        public ImageSource? Cover { get; init; }
        public string Name => Playlist.Name;
        public string Meta => $"{Playlist.TrackCount} canciones" + (string.IsNullOrEmpty(Playlist.Owner) ? "" : $" · {Playlist.Owner}");
    }

    private readonly TaskCompletionSource<SpotifyPlaylist?> _tcs = new();
    private List<PlaylistVm> _all = [];
    private bool _closed;

    public SpotifyPlaylistWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(700, 620));
        Closed += (_, _) => { if (!_closed) _tcs.TrySetResult(null); };
    }

    /// <summary>Muestra la ventana y resuelve con la playlist elegida (o null si se cancela).</summary>
    public Task<SpotifyPlaylist?> PickAsync()
    {
        Activate();
        _ = LoadAsync();
        return _tcs.Task;
    }

    private void Show(FrameworkElement panel)
    {
        ConnectPanel.Visibility = LoadingPanel.Visibility = ErrorPanel.Visibility = ListPanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
    }

    private async Task LoadAsync()
    {
        if (!SpotifyService.HasSession) { Show(ConnectPanel); return; }
        Show(LoadingPanel);
        try
        {
            var playlists = await SpotifyService.GetPlaylistsAsync();
            Populate(playlists);
        }
        catch
        {
            // El refresh falló (sesión caducada/revocada): pedir reconectar.
            Show(ConnectPanel);
        }
    }

    private void Populate(IReadOnlyList<SpotifyPlaylist> playlists)
    {
        _all = playlists.Select(p => new PlaylistVm
        {
            Playlist = p,
            Cover = string.IsNullOrEmpty(p.ImageUrl) ? null : new BitmapImage(new Uri(p.ImageUrl))
        }).ToList();
        ApplyFilter(SearchBox.Text);
        Show(ListPanel);
        if (_all.Count == 0)
        {
            ErrorText.Text = "No se encontraron playlists en tu cuenta.";
            Show(ErrorPanel);
        }
    }

    private void ApplyFilter(string? query)
    {
        var q = (query ?? "").Trim();
        IEnumerable<PlaylistVm> items = _all;
        if (q.Length > 0)
            items = _all.Where(v => v.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        PlaylistGrid.ItemsSource = items.ToList();
    }

    private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
    {
        Show(LoadingPanel);
        LoadingText.Text = "Abriendo el navegador para iniciar sesión…";
        bool ok;
        try { ok = await SpotifyService.AuthorizeAsync(); }
        catch { ok = false; }
        LoadingText.Text = "Cargando tus playlists…";
        if (ok) await LoadAsync();
        else { ErrorText.Text = "No se pudo conectar con Spotify. Inténtalo de nuevo."; Show(ErrorPanel); }
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

    private void Finish(SpotifyPlaylist? result)
    {
        _closed = true;
        _tcs.TrySetResult(result);
        Close();
    }
}
