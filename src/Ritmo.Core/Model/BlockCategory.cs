namespace Ritmo.Core.Model;

/// <summary>
/// Una categoría de bloque del horario, DEFINIBLE por el usuario (#83). Sustituye al
/// antiguo enum fijo <c>StudyKind</c>: cada categoría lleva su nombre, color, si dispara
/// concentración, y su orden. Las sesiones referencian una categoría por <see cref="Id"/>
/// (string estable), que es además la clave en el JSON (compatibilidad con los nombres
/// del enum legacy: "Tecnico", "Otro"…).
/// </summary>
public sealed record BlockCategory
{
    /// <summary>Id estable e inmutable (slug). Clave del JSON y referencia desde las sesiones.</summary>
    public required string Id { get; init; }
    /// <summary>Nombre visible (reemplaza a <c>StudyKind.Label()</c>).</summary>
    public required string Name { get; init; }
    /// <summary>Color de fondo "#RRGGBB".</summary>
    public required string ColorHex { get; init; }
    /// <summary>Color del texto "#RRGGBB" (opcional; si null la UI usa un gris).</summary>
    public string? TextColorHex { get; init; }
    /// <summary>¿Dispara el modo concentración? (reemplaza a <c>IsFocusKind()</c>).</summary>
    public bool IsFocus { get; init; }
    /// <summary>Orden de presentación.</summary>
    public int Order { get; init; }
    /// <summary>Categoría de sistema (no borrable): el fallback «Otro» y el hueco «Por definir».</summary>
    public bool IsSystem { get; init; }
}

/// <summary>Ids de las categorías de sistema (siempre presentes; no borrables).</summary>
public static class CategoryIds
{
    /// <summary>Fallback genérico cuando un bloque no tiene categoría decidida o válida.</summary>
    public const string Other = "Otro";
    /// <summary>Hueco reservado en el horario sin contenido decidido aún.</summary>
    public const string Undecided = "PorDefinir";
}
