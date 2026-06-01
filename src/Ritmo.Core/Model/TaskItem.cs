namespace Ritmo.Core.Model;

/// <summary>
/// Una tarea (#145), hija de un <see cref="TaskBlock"/>. Estilo Recordatorios de Apple: texto,
/// hecho/no hecho, orden, fecha opcional y notas. Puede vincularse a una sesión del horario
/// (<see cref="SessionKey"/>) cuando su bloque está asociado a un entorno (fase 3). Inmutable.
/// </summary>
public sealed record TaskItem
{
    /// <summary>Identificador estable.</summary>
    public required string Id { get; init; }
    /// <summary>Bloque (lista) al que pertenece.</summary>
    public required string BlockId { get; init; }
    /// <summary>Texto de la tarea.</summary>
    public required string Text { get; init; }
    /// <summary>¿Completada?</summary>
    public bool Done { get; init; }
    /// <summary>Orden dentro del bloque (menor = antes).</summary>
    public int Order { get; init; }
    /// <summary>Notas opcionales (texto libre).</summary>
    public string? Notes { get; init; }
    /// <summary>Fecha de vencimiento opcional.</summary>
    public System.DateOnly? DueDate { get; init; }
    /// <summary>
    /// Clave de la sesión a la que se asocia la tarea (#145 fase 3, ver <see cref="SessionKey.For"/>);
    /// null = no asociada a ninguna sesión. Permite el "cajón por sesión" y el atajo desde la sesión.
    /// </summary>
    public string? SessionKey { get; init; }
}
