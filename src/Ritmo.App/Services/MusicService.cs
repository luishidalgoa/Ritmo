using System;
using System.Diagnostics;
using Ritmo.Core.Focus;

namespace Ritmo_App.Services;

/// <summary>
/// Lanza la app de música configurada en un FocusEnvironment. Soporta tanto un
/// ejecutable con ruta (p. ej. Aonsoku.exe) como un protocolo/URI (p. ej.
/// "spotify:"). Es best-effort: si no se puede lanzar, no rompe la concentración.
/// </summary>
public static class MusicService
{
    /// <summary>Intenta lanzar la música del entorno. Devuelve true si lo lanzó.</summary>
    public static bool TryLaunch(MusicLauncher? music)
    {
        if (music is null || string.IsNullOrWhiteSpace(music.Target))
            return false;

        try
        {
            var target = music.Target.Trim();
            // UseShellExecute permite tanto rutas .exe como URIs de protocolo (spotify:, etc.).
            var psi = new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(music.Arguments))
                psi.Arguments = music.Arguments;

            Process.Start(psi);
            return true;
        }
        catch
        {
            // Ruta inválida, app no instalada, etc. No es fatal.
            return false;
        }
    }
}
