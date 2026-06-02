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
    /// <summary>
    /// Si la nota es un "post-it" de una sesión concreta, el título de esa sesión (#73).
    /// null = no está asociada a una sesión por título.
    /// </summary>
    public string? SessionTitle { get; init; }

    /// <summary>
    /// Si la nota está asociada a una CATEGORÍA de bloque (#153), su id (ver <see cref="BlockCategory.Id"/>).
    /// Aplica a TODAS las sesiones de esa categoría. Tiene prioridad sobre <see cref="SessionTitle"/>.
    /// null = no está asociada a ninguna categoría.
    /// </summary>
    public string? CategoryId { get; init; }

    /// <summary>Nota general/suelta: ni por título ni por categoría (aparece siempre en «Hoy»).</summary>
    public bool IsGeneral =>
        string.IsNullOrWhiteSpace(SessionTitle) && string.IsNullOrWhiteSpace(CategoryId);

    /// <summary>
    /// ¿Esta nota aplica a una sesión con el título y categoría dados? (#153)
    /// Una nota por categoría aplica a toda sesión de esa categoría; una por título solo a esa sesión.
    /// Las notas generales devuelven false (no son de una sesión concreta).
    /// </summary>
    public bool AppliesTo(string? sessionTitle, string? categoryId)
    {
        if (!string.IsNullOrWhiteSpace(CategoryId))
            return !string.IsNullOrWhiteSpace(categoryId)
                && string.Equals(CategoryId.Trim(), categoryId.Trim(), StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(SessionTitle))
            return !string.IsNullOrWhiteSpace(sessionTitle)
                && string.Equals(SessionTitle.Trim(), sessionTitle.Trim(), StringComparison.OrdinalIgnoreCase);
        return false;
    }
}
