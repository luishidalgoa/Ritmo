namespace Ritmo.Core.Model;

/// <summary>
/// Decisión del usuario ante un solapamiento sesión↔sesión (#149): marca una sesión
/// como PRIORITARIA en sus solapes. Es el equivalente de <see cref="OverlapPriority"/>
/// (que resuelve horario↔calendario) pero ENTRE sesiones del propio horario: cuando
/// varias chocan en una franja, las prioritarias se destacan y las no marcadas que
/// comparten ese conflicto recede. Pueden ser varias prioritarias a la vez. Solo
/// afecta a la presentación: no borra ni mueve nada.
/// </summary>
public sealed record SessionPriority
{
    /// <summary>
    /// Clave estable de la sesión marcada. Recurrente: "día|título|categoría|inicio|duración|provisional"
    /// (misma forma que la selección de la rejilla); extraordinaria (one-off): "one:" + su Id.
    /// </summary>
    public required string SessionKey { get; init; }
}
