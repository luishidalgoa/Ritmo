using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Notifications;
using Ritmo_App.Services;

namespace Ritmo_App.Dialogs;

/// <summary>
/// Modal "Conexiones" (#122): un único sitio para conectar Ritmo con apps externas
/// —notificaciones al móvil (ntfy) y, en el futuro, calendarios por OAuth (#112)—
/// para no saturar Ajustes. Lo que se active aquí se persiste vía ConfigurationService
/// y Ajustes solo muestra un resumen de lo activo.
/// </summary>
public sealed partial class ConnectionsDialog : ContentDialog
{
    public ConnectionsDialog()
    {
        InitializeComponent();
        var s = AppState.Load();
        NtfyEnabledToggle.IsOn = s.NtfyEnabled;
        NtfyServerBox.Text = s.NtfyServerUrl ?? "";
        NtfyTopicBox.Text = s.NtfyTopic ?? "";
        // Persistir al pulsar Guardar; si la validación falla, no cerrar y mostrar el error.
        PrimaryButtonClick += OnSave;
    }

    private void OnSave(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var r = AppState.Config.SetNtfy(NtfyEnabledToggle.IsOn, NtfyServerBox.Text, NtfyTopicBox.Text);
        if (!r.Success)
        {
            args.Cancel = true;
            ErrorText.Text = r.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void NtfyGenBtn_Click(object sender, RoutedEventArgs e)
        => NtfyTopicBox.Text = "ritmo-" + Guid.NewGuid().ToString("N").Substring(0, 10);

    private async void NtfyTestBtn_Click(object sender, RoutedEventArgs e)
    {
        var topic = (NtfyTopicBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(topic)) { NtfyStatus.Text = "Pon (o genera) un topic primero."; return; }

        NtfyStatus.Text = "Enviando…";
        NtfyTestBtn.IsEnabled = false;
        try
        {
            var pub = NtfyPublish.ForTest(NtfyServerBox.Text, topic);
            bool ok = await NtfyPublisher.PublishAsync(pub);
            NtfyStatus.Text = ok
                ? "✓ Enviado. Revisa el móvil suscrito a ese topic."
                : "⚠ No se pudo enviar (revisa servidor, topic y conexión).";
        }
        catch { NtfyStatus.Text = "⚠ Error al enviar la prueba."; }
        finally { NtfyTestBtn.IsEnabled = true; }
    }
}
