using System;
using Microsoft.UI.Xaml.Controls;

namespace Ritmo_App;

/// <summary>
/// Página «Hecho por» (créditos del autor): bio, enlaces (portfolio / GitHub / código del
/// proyecto), qué es Ritmo, contacto y el stack técnico. Solo abre enlaces externos.
/// </summary>
public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
    }

    private void OpenLink_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement fe && fe.Tag is string url && !string.IsNullOrWhiteSpace(url))
        {
            try { _ = Windows.System.Launcher.LaunchUriAsync(new Uri(url)); } catch { /* best-effort */ }
        }
    }
}
