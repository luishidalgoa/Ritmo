namespace Ritmo.Core.Model;

/// <summary>
/// Un PROYECTO/CLIENTE para el seguimiento laboral (#84 V3). Es un concepto PROPIO, independiente
/// de los entornos de concentración: muchos perfiles facturan por proyecto/cliente sin que eso
/// tenga que ver con su entorno de foco. Lleva su tarifa, objetivo mensual y color. Inmutable.
/// </summary>
public sealed record WorkProject
{
    /// <summary>Identificador estable (referenciado por <see cref="WorkLogEntry.ProjectId"/>).</summary>
    public required string Id { get; init; }
    /// <summary>Nombre visible (p. ej. «Cliente A», «Proyecto X»).</summary>
    public required string Name { get; init; }
    /// <summary>Color "#RRGGBB" para gráficos y chips.</summary>
    public string ColorHex { get; init; } = "#1E88E5";
    /// <summary>Tarifa por hora (en la moneda de <see cref="CurrencyCode"/>). 0 = sin tarifa.</summary>
    public decimal Rate { get; init; }
    /// <summary>Objetivo de horas al mes. 0 = sin objetivo.</summary>
    public double MonthlyGoalHours { get; init; }
    /// <summary>Código de moneda ISO opcional para mostrar (p. ej. "EUR", "USD"). Vacío = símbolo €.</summary>
    public string CurrencyCode { get; init; } = "EUR";
    /// <summary>Orden de presentación.</summary>
    public int Order { get; init; }
    /// <summary>¿Archivado? (se oculta de las vistas activas pero conserva su historial).</summary>
    public bool Archived { get; init; }

    /// <summary>
    /// Modo de cómputo de las sesiones VINCULADAS a este proyecto (#137): si es true, las horas de
    /// las sesiones del horario asociadas se suman AUTOMÁTICAMENTE los días que tocan (salvo
    /// excepciones). Si es false, Ritmo solo las SUGIERE para que el usuario las confirme.
    /// </summary>
    public bool AutoFromSchedule { get; init; } = true;

    /// <summary>Símbolo de moneda a partir del código (solo los comunes; si no, el propio código).</summary>
    public string CurrencySymbol => CurrencyCode switch
    {
        "EUR" or "" => "€",
        "USD" => "$",
        "GBP" => "£",
        "JPY" => "¥",
        "MXN" or "ARS" or "COP" or "CLP" => "$",
        _ => CurrencyCode
    };
}
