namespace Ritmo.Core.Focus;

/// <summary>
/// Qué app de música lanzar al entrar en concentración. Identifica el ejecutable
/// o el protocolo/URI (p. ej. Spotify: "spotify:", o ruta a Aonsoku).
/// </summary>
public sealed record MusicLauncher
{
    /// <summary>Nombre visible (p. ej. "Spotify", "Aonsoku").</summary>
    public required string Name { get; init; }
    /// <summary>Ejecutable, ruta o URI a lanzar (p. ej. "spotify:" o "C:\\...\\Aonsoku.exe").</summary>
    public required string Target { get; init; }
    /// <summary>Argumentos opcionales (p. ej. una playlist URI).</summary>
    public string? Arguments { get; init; }
    /// <summary>Si debe intentar empezar a reproducir automáticamente.</summary>
    public bool AutoPlay { get; init; }
}

/// <summary>
/// Un "entorno de concentración" reutilizable: describe QUÉ ocurre cuando el
/// usuario entra en modo focus con este perfil. Es un modelo de DATOS puro
/// (la ejecución real sobre el SO la hará la capa de integración en M4).
///
/// El usuario puede tener varios: "Estudio profundo", "Repaso ligero", "Simulacro"…
/// </summary>
public sealed record FocusEnvironment
{
    /// <summary>Identificador estable.</summary>
    public required string Id { get; init; }
    /// <summary>Nombre visible del entorno.</summary>
    public required string Name { get; init; }

    /// <summary>Nombre del preset Pomodoro a usar (p. ej. "DeepWork", "Classic"); null = el por defecto de la app.</summary>
    public string? PomodoroPreset { get; init; }

    /// <summary>Activar No molestar mientras dure la sesión.</summary>
    public bool EnableDoNotDisturb { get; init; } = true;
    /// <summary>Ocultar distintivos/parpadeos de la barra de tareas.</summary>
    public bool HideTaskbarBadges { get; init; } = true;
    /// <summary>Mostrar la vista previa del día al iniciar.</summary>
    public bool ShowDayPreview { get; init; } = true;
    /// <summary>Abrir (o crear) la lista de trabajo "Estudio" en Edge.</summary>
    public bool OpenStudyListInEdge { get; init; }

    /// <summary>Dominios de webs a bloquear durante la sesión (p. ej. "youtube.com").</summary>
    public IReadOnlyList<string> BlockedWebsites { get; init; } = [];
    /// <summary>Nombres de procesos/apps a cerrar al iniciar (p. ej. "Discord").</summary>
    public IReadOnlyList<string> AppsToClose { get; init; } = [];
    /// <summary>Nombres de procesos/apps a silenciar (sin cerrarlos).</summary>
    public IReadOnlyList<string> AppsToMute { get; init; } = [];

    /// <summary>App de música a lanzar (opcional).</summary>
    public MusicLauncher? Music { get; init; }

    /// <summary>
    /// Enlaces y herramientas del entorno (p. ej. el entorno "Oposiciones" agrupa
    /// el campus, el BOE, etc.). Accesos rápidos del "entorno de trabajo". #74
    /// </summary>
    public IReadOnlyList<Ritmo.Core.Model.ShortcutLink> Links { get; init; } = [];

    /// <summary>Preset cómodo: estudio profundo con todo el silencio activado.</summary>
    public static FocusEnvironment DeepStudy => new()
    {
        Id = "deep-study",
        Name = "Estudio profundo",
        PomodoroPreset = "DeepWork",
        EnableDoNotDisturb = true,
        HideTaskbarBadges = true,
        ShowDayPreview = true
    };
}
