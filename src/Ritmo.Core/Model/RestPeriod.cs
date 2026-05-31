namespace Ritmo.Core.Model;

/// <summary>
/// Periodo de descanso PROGRAMADO (#135): un rango de fechas (vacaciones, una pausa…) durante el
/// cual el horario NO lanza avisos. Inmutable y puro.
/// </summary>
public sealed record RestPeriod
{
    /// <summary>Identificador estable (para borrarlo).</summary>
    public required string Id { get; init; }
    /// <summary>Primer día del descanso (inclusive).</summary>
    public required System.DateOnly From { get; init; }
    /// <summary>Último día del descanso (inclusive).</summary>
    public required System.DateOnly To { get; init; }
    /// <summary>Etiqueta opcional (p. ej. «Vacaciones de verano»).</summary>
    public string Label { get; init; } = "";

    /// <summary>¿La fecha cae dentro del periodo (inclusive en ambos extremos)?</summary>
    public bool Covers(System.DateOnly d) => d >= From && d <= To;
}
