namespace Ritmo.Core.Pomodoro;

/// <summary>
/// Un "ritmo" Pomodoro con nombre: las duraciones + cada cuántos focos toca el
/// descanso largo, identificable para asignarlo a un entorno. Los de por defecto
/// (Clásico, Profundo) vienen con la app; el usuario puede crear los suyos en
/// Ajustes y elegirlos al crear un entorno. #96
/// </summary>
public sealed record PomodoroRhythm
{
    /// <summary>Identificador estable (los de por defecto: "classic", "deepwork").</summary>
    public required string Id { get; init; }
    /// <summary>Nombre visible.</summary>
    public required string Name { get; init; }
    public int FocusMinutes { get; init; } = 25;
    public int ShortBreakMinutes { get; init; } = 5;
    public int LongBreakMinutes { get; init; } = 15;
    public int FocusesPerLongBreak { get; init; } = 4;

    /// <summary>Si es uno de los ritmos de por defecto de la app (no se edita ni borra).</summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>Convierte este ritmo a la configuración del motor Pomodoro.</summary>
    public PomodoroConfig ToConfig() => new(
        TimeSpan.FromMinutes(FocusMinutes),
        TimeSpan.FromMinutes(ShortBreakMinutes),
        TimeSpan.FromMinutes(LongBreakMinutes),
        FocusesPerLongBreak);
}

/// <summary>
/// Ritmos Pomodoro disponibles: los de por defecto (fijos) más los del usuario.
/// Resuelve el id que guarda un entorno a una configuración de motor. #96
/// </summary>
public static class PomodoroRhythms
{
    public const string ClassicId = "classic";
    public const string DeepWorkId = "deepwork";

    /// <summary>Preset clásico: 25/5/15, descanso largo cada 4 focos.</summary>
    public static readonly PomodoroRhythm Classic = new()
    {
        Id = ClassicId, Name = "Clásico", IsBuiltIn = true,
        FocusMinutes = 25, ShortBreakMinutes = 5, LongBreakMinutes = 15, FocusesPerLongBreak = 4
    };

    /// <summary>Preset profundo (bloques largos): 50/10/20, largo cada 2 focos.</summary>
    public static readonly PomodoroRhythm DeepWork = new()
    {
        Id = DeepWorkId, Name = "Profundo", IsBuiltIn = true,
        FocusMinutes = 50, ShortBreakMinutes = 10, LongBreakMinutes = 20, FocusesPerLongBreak = 2
    };

    /// <summary>Los ritmos de por defecto de la app.</summary>
    public static IReadOnlyList<PomodoroRhythm> BuiltIns => [Classic, DeepWork];

    /// <summary>Todos los ritmos elegibles: los de por defecto + los del usuario.</summary>
    public static IReadOnlyList<PomodoroRhythm> All(IReadOnlyList<PomodoroRhythm> custom)
        => [.. BuiltIns, .. custom];

    /// <summary>
    /// Resuelve el ritmo guardado por un entorno (su id, p. ej. "classic", "deepwork"
    /// o el id de uno propio) a una configuración de motor. Acepta los nombres
    /// heredados ("Classic"/"DeepWork"). Si no resuelve nada (id vacío o desconocido),
    /// usa el Pomodoro por defecto de la app (<paramref name="appDefault"/>).
    /// </summary>
    public static PomodoroConfig Resolve(string? id, IReadOnlyList<PomodoroRhythm> custom, PomodoroConfig appDefault)
    {
        var rhythm = Find(id, custom);
        return rhythm?.ToConfig() ?? appDefault;
    }

    /// <summary>
    /// Busca un ritmo por id entre los de por defecto + los propios (sin distinguir
    /// mayúsculas), tolerando los nombres heredados. Null si el id es vacío/desconocido.
    /// </summary>
    public static PomodoroRhythm? Find(string? id, IReadOnlyList<PomodoroRhythm> custom)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var key = id.Trim();

        var match = All(custom).FirstOrDefault(r => string.Equals(r.Id, key, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        // Nombres heredados que guardaban los entornos antiguos.
        return key.ToLowerInvariant() switch
        {
            "classic" or "clasico" or "clásico" => Classic,
            "deepwork" or "deep work" or "deep" => DeepWork,
            _ => null
        };
    }
}
