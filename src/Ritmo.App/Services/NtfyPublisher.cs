using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ritmo.Core.Notifications;

namespace Ritmo_App.Services;

/// <summary>
/// Publica notificaciones en ntfy mediante un POST HTTP (modo JSON). El QUÉ se envía
/// lo decide el núcleo (<see cref="NtfyPublish"/>, puro y testeado); aquí solo se hace
/// la llamada de red. Best-effort: cualquier fallo se traga, nunca rompe el flujo de
/// avisos ni la UI. #122
/// </summary>
public static class NtfyPublisher
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>POST de la publicación. Devuelve true si el servidor respondió 2xx.</summary>
    public static async Task<bool> PublishAsync(NtfyPublication pub)
    {
        try
        {
            using var content = new StringContent(pub.JsonBody, Encoding.UTF8, "application/json");
            using var resp = await Http.PostAsync(pub.Url, content).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;   // sin red / servidor caído / topic inválido: best-effort
        }
    }
}
