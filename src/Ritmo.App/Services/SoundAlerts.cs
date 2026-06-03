using System;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Ritmo_App.Services;

/// <summary>
/// Avisos sonoros cortos del Pomodoro (#descansos): un chime al ENTRAR en descanso y otro al
/// REANUDAR la concentración, para que el cambio de fase no pase desapercibido. Reproduce WAVs
/// empaquetados (ms-appx) con un MediaPlayer reutilizado. Tolerante a fallos (nunca lanza).
/// </summary>
public static class SoundAlerts
{
    private static MediaPlayer? _player;

    private static void Play(string asset)
    {
        try
        {
            _player ??= new MediaPlayer { AudioCategory = MediaPlayerAudioCategory.Alerts };
            _player.Source = MediaSource.CreateFromUri(new Uri($"ms-appx:///Assets/{asset}"));
            _player.Play();
        }
        catch { /* sin sonido: no es crítico */ }
    }

    /// <summary>Chime descendente: ha empezado un descanso.</summary>
    public static void BreakStarted() => Play("break.wav");

    /// <summary>Chime ascendente: vuelve la concentración.</summary>
    public static void FocusResumed() => Play("resume.wav");
}
