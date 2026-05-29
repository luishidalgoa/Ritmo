using System.Text.Json;

namespace Ritmo.Core.Persistence;

/// <summary>
/// Serialización JSON de <see cref="AppSettings"/> compartida por el store y por
/// la exportación/importación de configuración (#56). Centraliza el formato
/// (camelCase, horas "HH:mm", fechas "yyyy-MM-dd") para que el archivo de respaldo
/// sea idéntico al settings.json.
/// </summary>
public static class SettingsJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Serializa la configuración a JSON legible.</summary>
    public static string Serialize(AppSettings settings)
        => JsonSerializer.Serialize(SettingsMapper.ToDto(settings), Options);

    /// <summary>
    /// Deserializa una configuración desde JSON. Lanza <see cref="JsonException"/>
    /// si el JSON es inválido; devuelve <see cref="AppSettings.Default"/> si está vacío.
    /// </summary>
    public static AppSettings Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return AppSettings.Default;
        var dto = JsonSerializer.Deserialize<SettingsDto>(json, Options);
        return dto is null ? AppSettings.Default : SettingsMapper.FromDto(dto);
    }
}
