namespace Ritmo.Core.Commands;

/// <summary>
/// Resultado de un comando de configuración. Pensado para que lo consuman por
/// igual la UI (mostrar errores) y la API/IA (responder de forma estructurada).
/// </summary>
public sealed record CommandResult
{
    public bool Success { get; init; }
    /// <summary>Mensaje legible (éxito o motivo del fallo).</summary>
    public string Message { get; init; } = "";
    /// <summary>Errores de validación, si los hubo.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static CommandResult Ok(string message = "OK") => new() { Success = true, Message = message };
    public static CommandResult Fail(params string[] errors) => new()
    {
        Success = false,
        Message = errors.Length > 0 ? errors[0] : "Error",
        Errors = errors
    };
}
