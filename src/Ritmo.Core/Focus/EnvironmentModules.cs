using System;
using System.Collections.Generic;
using System.Linq;

namespace Ritmo.Core.Focus;

/// <summary>
/// Los módulos que componen un «entorno» como contexto de trabajo (#75/#76).
/// Un entorno se presenta como un colapsable con estos módulos; al pulsar uno se
/// abre su vista de detalle.
/// </summary>
public enum EnvironmentModuleKind
{
    /// <summary>Concentración: música, apps, No molestar, preset Pomodoro (#53).</summary>
    Focus,
    /// <summary>Enlaces y accesos rápidos del entorno (#74).</summary>
    Links,
    /// <summary>Lista de tareas propia del entorno (#77).</summary>
    Tasks,
    /// <summary>Herramientas externas: calendario, abrir el workspace en el navegador.</summary>
    Tools
}

/// <summary>
/// Descriptor (PURO) de un módulo para pintarlo en la lista colapsable de un entorno:
/// título, resumen de su estado y si ya es accionable (los módulos aún sin editor se
/// muestran como «Próximamente»). No conoce nada de la UI.
/// </summary>
public sealed record EnvironmentModuleInfo
{
    public required EnvironmentModuleKind Kind { get; init; }
    /// <summary>Nombre visible del módulo.</summary>
    public required string Title { get; init; }
    /// <summary>Resumen corto del estado del módulo para ese entorno.</summary>
    public required string Summary { get; init; }
    /// <summary>Si el módulo ya tiene editor (true) o es un placeholder «Próximamente» (false).</summary>
    public required bool Available { get; init; }
}

/// <summary>
/// Deriva, a partir de un <see cref="FocusEnvironment"/>, los módulos que lo componen
/// con su resumen. PURO y testeable; lo consume el panel de entornos (Studio/Flutter).
/// </summary>
public static class EnvironmentModules
{
    /// <summary>Texto que se muestra cuando un módulo aún no tiene editor.</summary>
    public const string ComingSoon = "Próximamente";

    /// <summary>Los 4 módulos del entorno, en orden de presentación, con su resumen.</summary>
    public static IReadOnlyList<EnvironmentModuleInfo> For(FocusEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);
        return
        [
            new EnvironmentModuleInfo
            {
                Kind = EnvironmentModuleKind.Focus, Title = "Concentración",
                Summary = FocusSummary(env), Available = true
            },
            new EnvironmentModuleInfo
            {
                Kind = EnvironmentModuleKind.Links, Title = "Enlaces",
                Summary = LinksSummary(env), Available = true
            },
            // Tareas (#77/#125): lista de to-dos propia del entorno.
            new EnvironmentModuleInfo
            {
                Kind = EnvironmentModuleKind.Tasks, Title = "Tareas",
                Summary = TasksSummary(env), Available = true
            },
            // Herramientas externas (#78): por ahora «abrir el workspace en el navegador».
            new EnvironmentModuleInfo
            {
                Kind = EnvironmentModuleKind.Tools, Title = "Herramientas externas",
                Summary = ToolsSummary(env), Available = true
            },
        ];
    }

    /// <summary>Resumen del módulo Concentración (qué pasa al concentrarte).</summary>
    public static string FocusSummary(FocusEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);
        var parts = new List<string>();
        if (env.EnableDoNotDisturb) parts.Add("No molestar");
        if (env.Music is not null) parts.Add("música");
        if (env.AppsToOpen.Count > 0) parts.Add($"abre {env.AppsToOpen.Count} app(s)");
        if (env.AppsToClose.Count > 0) parts.Add($"cierra {env.AppsToClose.Count}");
        if (env.BlockedWebsites.Count > 0) parts.Add($"bloquea {env.BlockedWebsites.Count} web(s)");
        return parts.Count == 0 ? "Sin acciones extra" : string.Join(" · ", parts);
    }

    /// <summary>Resumen del módulo Enlaces.</summary>
    public static string LinksSummary(FocusEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);
        return env.Links.Count switch
        {
            0 => "Sin enlaces",
            1 => "1 enlace",
            var n => $"{n} enlaces"
        };
    }

    /// <summary>Resumen del módulo Tareas (#77/#125).</summary>
    public static string TasksSummary(FocusEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);
        if (env.Tasks.Count == 0) return "Sin tareas";
        var pending = env.Tasks.Count(t => !t.Done);
        return pending == 0
            ? $"Todas hechas ({env.Tasks.Count})"
            : $"{pending}/{env.Tasks.Count} pendientes";
    }

    /// <summary>Resumen del módulo Herramientas externas (#78).</summary>
    public static string ToolsSummary(FocusEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);
        var n = EnvironmentWorkspace.Urls(env).Count;
        return n == 0
            ? "Abrir el workspace en el navegador (añade enlaces)"
            : $"Abrir el workspace: {n} enlace(s) en el navegador";
    }
}
