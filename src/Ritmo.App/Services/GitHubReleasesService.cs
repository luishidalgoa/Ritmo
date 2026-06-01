using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ritmo_App.Services;

/// <summary>Última release publicada en GitHub (versión derivada del tag + URL de la página y del .appinstaller).</summary>
internal sealed record LatestRelease(string Version, string Tag, string Url, string? AppInstallerUrl);

/// <summary>
/// Consulta la GitHub Releases API para saber si hay una versión más nueva (#updates,
/// Fase 3). Es solo INFORMATIVO: la instalación de la actualización la hace App Installer
/// de forma nativa vía el .appinstaller. Best-effort: si no hay red/releases, devuelve null.
/// </summary>
internal static class GitHubReleasesService
{
    private const string Owner = "luishidalgoa";
    private const string Repo = "Ritmo";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Ritmo-App");        // la API de GitHub lo exige
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    /// <summary>
    /// Última release NO prerelease. null si no hay releases (404) o falla la red.
    /// La versión se deriva del tag (`vX.Y.Z` → `X.Y.Z`).
    /// </summary>
    public static async Task<LatestRelease?> GetLatestAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            using var resp = await Http.GetAsync(url).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;   // 404 = aún sin releases

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var htmlUrl = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(tag)) return null;

            // URL de descarga del asset .appinstaller (lo que App Installer abre para actualizar).
            string? appInstaller = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (name is not null && name.EndsWith(".appinstaller", StringComparison.OrdinalIgnoreCase))
                    {
                        appInstaller = a.TryGetProperty("browser_download_url", out var d) ? d.GetString() : null;
                        break;
                    }
                }

            return new LatestRelease(tag.TrimStart('v', 'V'), tag, htmlUrl, appInstaller);
        }
        catch
        {
            return null;   // best-effort
        }
    }
}
