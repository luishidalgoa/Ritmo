using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Ritmo.Core.Updates;
using Windows.ApplicationModel;

namespace Ritmo_App.Services;

/// <summary>
/// Auto-actualización ESTILO DISCORD (#updates). La app DESCARGA el .msix nuevo y lo aplica con un
/// script PowerShell EXTERNO que: (1) espera a que Ritmo cierre, (2) hace <c>Add-AppxPackage</c> del
/// paquete nuevo —PER-USER, sin admin/UAC, sin App Installer ni el protocolo <c>ms-appinstaller</c>
/// (que Microsoft desactivó por seguridad)—, y (3) reabre Ritmo ya actualizada. Evita el error
/// <c>0x80073D02</c> ("paquete en uso") porque la app está CERRADA cuando se instala.
///
/// Flujo: al arrancar se descarga la versión nueva (staged); al SIGUIENTE arranque (o al pulsar
/// "Instalar ahora") se aplica y reinicia. Guard de un intento por versión para no entrar en bucle.
/// </summary>
internal static class SelfUpdater
{
    // Nombre ESTABLE del .msix en cada release (el workflow lo publica así).
    private const string MsixUrl = "https://github.com/luishidalgoa/Ritmo/releases/latest/download/Ritmo-x64.msix";

    private static string UpdateDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ritmo", "update");

    /// <summary>Ruta de un .msix YA descargado más nuevo que la versión actual (pendiente de aplicar), o null.</summary>
    public static string? PendingUpdate()
    {
        try
        {
            if (!Directory.Exists(UpdateDir)) return null;
            foreach (var f in Directory.GetFiles(UpdateDir, "Ritmo-*.msix"))
            {
                var ver = Path.GetFileNameWithoutExtension(f)["Ritmo-".Length..];
                if (ReleaseNotes.CompareVersions(ver, AppVersionInfo.Current) > 0) return f;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    private static string TriedMarker(string msixPath) => msixPath + ".tried";
    /// <summary>¿Ya se intentó aplicar este .msix (y sigue pendiente → falló)? Para no reintentar en bucle.</summary>
    public static bool AlreadyTried(string msixPath) => File.Exists(TriedMarker(msixPath));
    public static void MarkTried(string msixPath) { try { File.WriteAllText(TriedMarker(msixPath), ""); } catch { } }

    /// <summary>
    /// Si hay una release más nueva, DESCARGA su .msix y lo deja staged (no aplica nada). Devuelve la
    /// ruta descargada (o la ya existente), o null si está al día / falla. Best-effort.
    /// </summary>
    public static async Task<string?> DownloadIfNewerAsync()
    {
        try
        {
            var latest = await GitHubReleasesService.GetLatestAsync().ConfigureAwait(false);
            if (latest is null || ReleaseNotes.CompareVersions(latest.Version, AppVersionInfo.Current) <= 0)
                return null;

            Directory.CreateDirectory(UpdateDir);
            var path = Path.Combine(UpdateDir, $"Ritmo-{latest.Version}.msix");
            if (!File.Exists(path) || new FileInfo(path).Length < 1_000_000)
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(8) };
                var tmp = path + ".part";
                var bytes = await http.GetByteArrayAsync(MsixUrl).ConfigureAwait(false);
                await File.WriteAllBytesAsync(tmp, bytes).ConfigureAwait(false);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            Log($"descargado {Path.GetFileName(path)} ({new FileInfo(path).Length / 1_000_000}MB)");
            return path;
        }
        catch (Exception ex) { Log($"download error: {ex.GetType().Name}: {ex.Message}"); return null; }
    }

    /// <summary>
    /// Aplica un .msix descargado: lanza un script PowerShell EXTERNO (espera el cierre → Add-AppxPackage
    /// → reabre) y CIERRA Ritmo para que el paquete no esté en uso. No retorna control útil.
    /// </summary>
    public static void ApplyAndRestart(string msixPath)
    {
        try
        {
            var appId = $"{Package.Current.Id.FamilyName}!App";
            var scriptPath = Path.Combine(UpdateDir, "apply-update.ps1");
            var lines = new[]
            {
                "$ErrorActionPreference = 'SilentlyContinue'",
                // Esperar (hasta 30s) a que Ritmo cierre del todo.
                "for ($i=0; $i -lt 60; $i++) { if (-not (Get-Process 'Ritmo.App')) { break }; Start-Sleep -Milliseconds 500 }",
                "Start-Sleep -Milliseconds 800",
                // Instalar la versión nueva (per-user, sin admin; la app cerrada → no 0x80073D02).
                $"Add-AppxPackage -Path '{msixPath}' -ForceApplicationShutdown",
                // Reabrir Ritmo ya actualizada.
                $"Start-Process 'shell:AppsFolder\\{appId}'",
                // Limpiar siempre (éxito o fallo) para no reintentar en bucle.
                $"Remove-Item '{msixPath}' -Force",
                $"Remove-Item '{TriedMarker(msixPath)}' -Force",
                $"Remove-Item '{scriptPath}' -Force",
            };
            File.WriteAllText(scriptPath, string.Join("\r\n", lines));

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            Log("aplicando + reiniciando");
            Application.Current.Exit();   // cerrar para que el script pueda reemplazar el paquete
        }
        catch (Exception ex) { Log($"apply error: {ex.GetType().Name}: {ex.Message}"); }
    }

    private static void Log(string msg)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ritmo", "update-error.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} v{AppVersionInfo.Current} {msg}{Environment.NewLine}");
        }
        catch { /* best-effort */ }
    }
}
