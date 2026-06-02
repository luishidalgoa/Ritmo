namespace Ritmo.Core.Model;

/// <summary>
/// Bloque de tareas (#145), al estilo de una "lista" de Recordatorios de Apple: agrupa tareas
/// (<see cref="TaskItem"/>) bajo un nombre y color. Puede ir por libre o vincularse a un entorno
/// (<see cref="EnvironmentId"/>): en ese caso el bloque organiza sus tareas por las sesiones del
/// entorno (fase 3). Inmutable.
/// </summary>
public sealed record TaskBlock
{
    /// <summary>Identificador estable.</summary>
    public required string Id { get; init; }
    /// <summary>Nombre visible del bloque/lista.</summary>
    public required string Name { get; init; }
    /// <summary>Color de acento (hex "#RRGGBB"), opcional.</summary>
    public string? ColorHex { get; init; }
    /// <summary>Orden de aparición (menor = antes).</summary>
    public int Order { get; init; }
    /// <summary>
    /// Id del entorno al que se vincula el bloque (#145 fase 3); null = bloque suelto. Cuando hay
    /// vínculo, las tareas se agrupan en "cajones" por cada sesión del entorno.
    /// </summary>
    public string? EnvironmentId { get; init; }

    /// <summary>Id de la lista externa con la que está sincronizado este bloque (#64); null = no sincronizado.</summary>
    public string? ExternalId { get; init; }

    /// <summary>
    /// Proveedor externo dueño de <see cref="ExternalId"/> (#64): "google" (único por ahora).
    /// null = sin vincular todavía (lo reclama la primera sincronización). Deja la puerta abierta a más
    /// proveedores sin que la sincronización de uno pise los ids de otro.
    /// </summary>
    public string? Provider { get; init; }
}
