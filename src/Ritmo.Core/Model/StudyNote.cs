namespace Ritmo.Core.Model;

/// <summary>
/// Una nota fijada por el usuario, con propósito propio (como las cajas
/// "¡OJO! Importante" / "No olvidar" del Excel, pero el usuario crea las que
/// quiera). El contenido admite formato markdown. Inmutable.
/// </summary>
public sealed record StudyNote
{
    /// <summary>Identificador estable (para editarla/borrarla sin ambigüedad).</summary>
    public required string Id { get; init; }
    /// <summary>Título o propósito de la nota (p. ej. "¡OJO! Importante").</summary>
    public required string Title { get; init; }
    /// <summary>Contenido en markdown.</summary>
    public string Content { get; init; } = "";
    /// <summary>Color de acento de la nota (hex "#RRGGBB"), opcional.</summary>
    public string? AccentColor { get; init; }
    /// <summary>Orden de aparición (menor = antes).</summary>
    public int Order { get; init; }
}
