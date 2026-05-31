using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Notifications;

namespace Ritmo_App.Dialogs;

/// <summary>
/// Guía visual (carrusel) de cómo suscribir el móvil al topic de ntfy (#123). Pasos
/// con ilustración + texto, navegables por swipe/flechas y con puntos (PipsPager).
/// Recibe el topic y el servidor para mostrarlos en el paso correspondiente.
/// </summary>
public sealed partial class NtfyGuideDialog : ContentDialog
{
    public NtfyGuideDialog(string topic, string server)
    {
        InitializeComponent();
        GuideTopicBox.Text = topic;

        // Solo se enseña la nota de "servidor propio" si NO es el ntfy.sh por defecto.
        var normalized = NtfyPublish.NormalizeServer(server);
        if (!normalized.Equals(NtfyPublish.DefaultServer, System.StringComparison.OrdinalIgnoreCase))
        {
            GuideServerNote.Text = $"Servidor propio: en la app activa «Use another server» y pon {normalized}";
            GuideServerNote.Visibility = Visibility.Visible;
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dp.SetText(GuideTopicBox.Text ?? "");
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
    }
}
