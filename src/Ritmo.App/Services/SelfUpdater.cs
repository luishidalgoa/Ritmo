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

            return result is null || string.IsNullOrEmpty(result.ErrorText);
        }
        catch
        {
            return false;   // best-effort: queda el botón manual de Ajustes
        }
    }
}
