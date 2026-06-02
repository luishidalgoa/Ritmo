using System;
using System.IO;

namespace Ritmo_App.Services;

/// <summary>
/// Prepara la extensión de bloqueo (#8) para que el usuario la cargue desempaquetada. La extensión
/// se empaqueta con la app (solo lectura), así que la COPIAMOS a una carpeta accesible del usuario
/// (%LOCALAPPDATA%\Ritmo\blocker-extension) desde donde el navegador puede cargarla.
/// </summary>
internal static class BlockerExtension
{
    private static readonly string[] Files = { "manifest.json", "background.js", "README.md" };

    /// <summary>Carpeta destino (accesible por el usuario) donde se deja la extensión.</summary>
    public static string UserFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ritmo", "blocker-extension");

    /// <summary>Copia la extensión a la carpeta del usuario y devuelve su ruta. Lanza si no encuentra el origen.</summary>
    public static string PrepareToUserFolder()
    {
        var src = Path.Combine(AppContext.BaseDirectory, "Assets", "ritmo-blocker");
        var dst = UserFolder;
        Directory.CreateDirectory(dst);
        foreach (var f in Files)
        {
            var s = Path.Combine(src, f);
            if (File.Exists(s)) File.Copy(s, Path.Combine(dst, f), overwrite: true);
        }
        return dst;
    }
}
