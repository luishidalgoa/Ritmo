using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo_App.Services;

namespace Ritmo_App.Dialogs;

/// <summary>
/// Modal de DESCUBRIMIENTO de conexiones (#123), al estilo del catálogo de conectores
/// de Claude: aquí el usuario ve qué apps externas puede conectar y AÑADE una. La
/// gestión del día a día (activar/pausar, editar, probar, eliminar) vive en
/// Ajustes › Conexiones, sin necesidad de volver a abrir este modal.
/// </summary>
public sealed partial class ConnectionsDialog : ContentDialog
{
    public ConnectionsDialog()
    {
        InitializeComponent();
        // Si ntfy ya está conectada, no se vuelve a "Conectar": se gestiona en Ajustes.
        bool connected = !string.IsNullOrWhiteSpace(AppState.Load().NtfyTopic);
        NtfyConnectBtn.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
        NtfyAlreadyText.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Crea la conexión ntfy con un topic privado generado y el servidor público por
    /// defecto, la deja activa y cierra el modal. El usuario la verá y refinará
    /// (servidor propio, prueba, etc.) en Ajustes › Conexiones.
    /// </summary>
    private void NtfyConnectBtn_Click(object sender, RoutedEventArgs e)
    {
        var topic = "ritmo-" + Guid.NewGuid().ToString("N").Substring(0, 10);
        AppState.Config.SetNtfy(true, "https://ntfy.sh", topic);
        Hide();
    }
}
