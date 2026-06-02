namespace Ritmo.Core.Model;

/// <summary>
/// "Lápida" de una tarea ya sincronizada que se BORRÓ en local (#64): recuerda qué tarea remota hay
/// que borrar en el proveedor en la próxima sincronización. Sin esto, el borrado local no se propaga
/// y, peor, la tarea remota se vuelve a traer (PullNew) y reaparece. Se elimina al confirmar el borrado
/// remoto (o si ya no existe allí).
/// </summary>
public sealed record TaskTombstone
{
    /// <summary>Proveedor dueño: "google" | "apple".</summary>
    public required string Provider { get; init; }
    /// <summary>Id de la lista remota (el ExternalId del bloque).</summary>
    public required string ListId { get; init; }
    /// <summary>Id de la tarea remota a borrar (el ExternalId de la tarea).</summary>
    public required string TaskId { get; init; }
}
