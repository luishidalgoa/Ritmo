using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ritmo.Core.Focus;

namespace Ritmo_App.Services;

/// <summary>
/// Abre Microsoft Edge en un perfil dedicado de estudio/concentración.
/// La DECISIÓN (reutilizar vs crear, qué nombre) vive en el núcleo puro
/// (<see cref="EdgeProfileResolver"/>, ya testeado). Aquí solo está el IO:
/// leer/escribir el Local State de Edge y lanzar el navegador.
/// Best-effort: si Edge no está o algo falla, no rompe la concentración.
/// </summary>
public static class EdgeStudyProfile
{
    private static string UserDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "Microsoft", "Edge", "User Data");

    /// <summary>Abre Edge en el perfil de estudio (lo crea si no existe).</summary>
    public static bool OpenStudyProfile()
    {
        try
        {
            var localState = Path.Combine(UserDataDir, "Local State");
            if (!File.Exists(localState)) return false;

            var root = JsonNode.Parse(File.ReadAllText(localState))!;
            var cache = root["profile"]?["info_cache"]?.AsObject();
            if (cache is null) return false;

            // folder → nombre visible, para el resolver puro.
            var profiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in cache)
                profiles[kv.Key] = kv.Value?["name"]?.GetValue<string>() ?? "";

            // Carpetas en disco que aún no estén en el cache (evita colisiones al crear).
            var onDisk = Directory.Exists(UserDataDir)
                ? Directory.GetDirectories(UserDataDir).Select(Path.GetFileName).Where(n => n is not null)!
                : Enumerable.Empty<string>();

            var decision = EdgeProfileResolver.Resolve(
                profiles,
                CultureInfo.CurrentUICulture.Name,
                onDisk!);

            if (decision.NeedsCreation)
            {
                cache[decision.Folder] = new JsonObject { ["name"] = decision.DisplayName };
                File.WriteAllText(localState, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
            }

            return LaunchEdge(decision.Folder);
        }
        catch
        {
            return false;
        }
    }

    private static bool LaunchEdge(string profileFolder)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "msedge.exe",
            UseShellExecute = true,
            Arguments = $"--profile-directory=\"{profileFolder}\""
        };
        try { Process.Start(psi); return true; }
        catch
        {
            // Fallback por si msedge.exe no está en PATH: ruta típica.
            var edge = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft", "Edge", "Application", "msedge.exe");
            if (!File.Exists(edge)) return false;
            psi.FileName = edge;
            Process.Start(psi);
            return true;
        }
    }
}
