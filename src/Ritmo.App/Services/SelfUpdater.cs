using System;
using System.Threading.Tasks;
using Ritmo.Core.Updates;
using Windows.Management.Deployment;

namespace Ritmo_App.Services;

/// <summary>
/// Auto-actualización SILENCIOSA (#updates). Al abrir, si hay una versión más nueva publicada, la app
/// se actualiza A SÍ MISMA vía <see cref="PackageManager"/> (sin App Installer ni botón). El paquete
/// nuevo se descarga/prepara en segundo plano y Windows lo aplica al SIGUIENTE arranque (no se puede
/// reemplazar el binario en marcha). Funciona se haya instalado como se haya instalado (no depende del
/// canal nativo del .appinstaller). Best-effort: si falla (sin red, API, permisos), NO rompe nada y
/// queda el botón manual de Ajustes como respaldo.
/// </summary>
internal static class SelfUpdater
{
    /// <summary>
    /// Comprueba la última release y, si es más nueva, lanza la actualización en segundo plano.
    /// Devuelve true si se ha PUESTO EN MARCHA (se aplicará al reabrir), false si ya está al día o no
    /// se pudo. No lanza: cualquier fallo se traga (best-effort).
    /// </summary>
    public static async Task<bool> TryUpdateAsync()
    {
        try
        {
            var latest = await GitHubReleasesService.GetLatestAsync().ConfigureAwait(false);
            if (latest is null || string.IsNullOrWhiteSpace(latest.AppInstallerUrl)) return false;
            if (ReleaseNotes.CompareVersions(latest.Version, AppVersionInfo.Current) <= 0) return false;

            var pm = new PackageManager();
            // None: descarga/prepara y aplica al próximo arranque (no fuerza cerrar la app en marcha).
            var result = await pm.AddPackageByAppInstallerFileAsync(
                new Uri(latest.AppInstallerUrl!),
                AddPackageByAppInstallerOptions.None,
                targetVolume: null);

            if (result is not null && !string.IsNullOrEmpty(result.ErrorText))
            {
                Log($"deploy-error hr=0x{result.ExtendedErrorCode?.HResult:X8} : {result.ErrorText}");
                return false;
            }
            Log("ok: actualizacion preparada");
            return true;
        }
        catch (Exception ex)
        {
            Log($"exception {ex.GetType().Name} hr=0x{ex.HResult:X8} : {ex.Message}");
            return false;   // best-effort: queda el botón manual de Ajustes
        }
    }

    /// <summary>
    /// Apunta el motivo del fallo/éxito del auto-update a %USERPROFILE%\.ritmo\update-error.log,
    /// para diagnosticar por qué la vía silenciosa no aplica en algunas instalaciones. Best-effort.
    /// </summary>
    private static void Log(string msg)
    {
        try
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ritmo", "update-error.log");
            System.IO.File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} v{AppVersionInfo.Current} {msg}{Environment.NewLine}");
        }
        catch { /* best-effort */ }
    }
}
