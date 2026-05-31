namespace Ritmo.Core.Pomodoro;

/// <summary>
/// Configuración de un ciclo Pomodoro: duraciones y cada cuántos focos
/// toca un descanso largo. Inmutable y validada en construcción.
/// </summary>
public sealed record PomodoroConfig
{
    /// <summary>Duración de un bloque de concentración.</summary>
    public TimeSpan Focus { get; }
    /// <summary>Duración del descanso corto (tras un foco normal).</summary>
    public TimeSpan ShortBreak { get; }
    /// <summary>Duración del descanso largo.</summary>
    public TimeSpan LongBreak { get; }
    /// <summary>Cada cuántos focos completados toca un descanso largo (p. ej. 4).</summary>
    public int FocusesPerLongBreak { get; }

    public PomodoroConfig(TimeSpan focus, TimeSpan shortBreak, TimeSpan longBreak, int focusesPerLongBreak)
    {
        if (focus <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(focus), "La concentración debe durar más de 0.");
        if (shortBreak < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(shortBreak), "El descanso corto no puede ser negativo.");
        if (longBreak < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(longBreak), "El descanso largo no puede ser negativo.");
        if (focusesPerLongBreak < 1)
            throw new ArgumentOutOfRangeException(nameof(focusesPerLongBreak), "Debe haber al menos 1 foco por descanso largo.");

        Focus = focus;
        ShortBreak = shortBreak;
        LongBreak = longBreak;
        FocusesPerLongBreak = focusesPerLongBreak;
    }

    /// <summary>Preset clásico: 25/5/15, descanso largo cada 4 focos.</summary>
    public static PomodoroConfig Classic => new(
        TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), 4);

    /// <summary>Preset largo (encaja con bloques de ~2h): 50/10/20, largo cada 2.</summary>
    public static PomodoroConfig DeepWork => new(
        TimeSpan.FromMinutes(50), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20), 2);

    /// <summary>
    /// Resuelve un preset por su nombre (el que guarda un entorno de concentración,
    /// p. ej. "DeepWork" o "Classic"). Si el nombre no coincide con ningún preset,
    /// devuelve <paramref name="fallback"/> (o <see cref="DeepWork"/> si es null).
    /// </summary>
    public static PomodoroConfig ByName(string? name, PomodoroConfig? fallback = null) =>
        name?.Trim().ToLowerInvariant() switch
        {
            "classic" or "clasico" or "clásico" => Classic,
            "deepwork" or "deep work" or "deep" => DeepWork,
            _ => fallback ?? DeepWork
        };
}
