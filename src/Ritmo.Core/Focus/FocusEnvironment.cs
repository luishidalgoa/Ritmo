using System;
using System.Collections.Generic;
using System.Linq;

namespace Ritmo.Core.Focus;

/// <summary>
/// Perfil de apertura POR TIPO de sesión (agrupada por título) dentro de un entorno
/// (#116). Indica qué subconjunto de los enlaces y de las apps-a-abrir del entorno
/// se activan al concentrarse en una sesión de ese título. Si un título no tiene
/// perfil, se abre TODO (comportamiento por defecto del entorno).
/// </summary>
public sealed record SessionAppProfile
{
    /// <summary>Título de la sesión (normalizado con Trim), clave del grupo.</summary>
    public required string SessionTitle { get; init; }
    /// <summary>URLs de los enlaces del entorno que SÍ se abren para este tipo.</summary>
    public IReadOnlyList<string> EnabledLinks { get; init; } = [];
    /// <summary>Nombres de proceso de las apps-a-abrir del entorno que SÍ se abren.</summary>
    public IReadOnlyList<string> EnabledApps { get; init; } = [];
}

/// <summary>Una tarea (to-do) propia de un entorno de trabajo. #77</summary>
public sealed record EnvironmentTask
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public bool Done { get; init; }
    public int Order { get; init; }
}

/// <summary>
/// Qué música lanzar al entrar en concentración. Puede ser una app/URI a lanzar
/// (<see cref="Target"/>) o un proveedor configurable como Navidrome (#107).
/// </summary>
public sealed record MusicLauncher
{
    /// <summary>Nombre visible (p. ej. "Navidrome", "Spotify").</summary>
    public required string Name { get; init; }
    /// <summary>Ejecutable, ruta o URL a lanzar (p. ej. la URL web de la playlist).</summary>
    public required string Target { get; init; }
    /// <summary>Argumentos opcionales.</summary>
    public string? Arguments { get; init; }
    /// <summary>Si debe intentar empezar a reproducir automáticamente.</summary>
    public bool AutoPlay { get; init; }

    /// <summary>Proveedor: "navidrome" (servidor Subsonic) o null (app/URI directa). #107</summary>
    public string? Provider { get; init; }
    /// <summary>Id de la playlist elegida (el servidor/usuario son globales). #107</summary>
    public string? PlaylistId { get; init; }
    /// <summary>Nombre de la playlist (para mostrar).</summary>
    public string? PlaylistName { get; init; }
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
    /// <summary>Abrir los enlaces del entorno en una ventana nueva del navegador por defecto. #109</summary>
    public bool OpenLinksInBrowser { get; init; }

    /// <summary>Dominios de webs a bloquear durante la sesión (p. ej. "youtube.com").</summary>
    public IReadOnlyList<string> BlockedWebsites { get; init; } = [];
    /// <summary>Nombres de procesos/apps a cerrar al iniciar (p. ej. "Discord").</summary>
    public IReadOnlyList<string> AppsToClose { get; init; } = [];
    /// <summary>Nombres de procesos/apps a silenciar (sin cerrarlos).</summary>
    public IReadOnlyList<string> AppsToMute { get; init; } = [];
    /// <summary>Apps a ABRIR al concentrarte (herramientas de estudio). #109</summary>
    public IReadOnlyList<string> AppsToOpen { get; init; } = [];

    /// <summary>Crear un escritorio virtual de Windows nuevo al concentrarte (aísla el contexto). #110</summary>
    public bool NewVirtualDesktop { get; init; }

    /// <summary>App de música a lanzar (opcional).</summary>
    public MusicLauncher? Music { get; init; }

    /// <summary>
    /// Enlaces y herramientas del entorno (p. ej. el entorno "Oposiciones" agrupa
    /// el campus, el BOE, etc.). Accesos rápidos del "entorno de trabajo". #74
    /// </summary>
    public IReadOnlyList<Ritmo.Core.Model.ShortcutLink> Links { get; init; } = [];

    /// <summary>Lista de tareas (to-do) propia del entorno. #77</summary>
    public IReadOnlyList<EnvironmentTask> Tasks { get; init; } = [];

    /// <summary>
    /// Perfiles por tipo de sesión (título): qué enlaces/apps de los de arriba se
    /// abren para cada tipo. Vacío = todos los tipos abren todo (por defecto). #116
    /// </summary>
    public IReadOnlyList<SessionAppProfile> SessionProfiles { get; init; } = [];

    /// <summary>
    /// Qué enlaces y apps abrir al concentrarse en una sesión con el título dado.
    /// Si existe un perfil para ese título, devuelve solo su subconjunto (intersecado
    /// con los actuales, por si se quitaron enlaces/apps); si no, devuelve TODO. #116
    /// </summary>
    public (IReadOnlyList<Ritmo.Core.Model.ShortcutLink> Links, IReadOnlyList<string> Apps) ResolveOpen(string? sessionTitle)
    {
        if (string.IsNullOrWhiteSpace(sessionTitle)) return (Links, AppsToOpen);
        var key = sessionTitle.Trim();
        var profile = SessionProfiles.FirstOrDefault(
            p => string.Equals(p.SessionTitle.Trim(), key, StringComparison.OrdinalIgnoreCase));
        if (profile is null) return (Links, AppsToOpen);

        var links = Links.Where(l => profile.EnabledLinks.Contains(l.Url, StringComparer.OrdinalIgnoreCase)).ToList();
        var apps = AppsToOpen.Where(a => profile.EnabledApps.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList();
        return (links, apps);
    }

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
