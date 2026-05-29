using System.Text.Json;

namespace Ritmo.Core.Persistence;

/// <summary>Carga y guarda el estado de la app. Abstrae el "dónde".</summary>
public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

/// <summary>
/// Persistencia en un archivo JSON. Si el archivo no existe o está corrupto,
/// Load devuelve AppSettings.Default (la app nunca arranca rota por el disco).
/// El JSON es legible y editable a mano (horas "HH:mm", duraciones en minutos).
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string FilePath { get; }

    public JsonSettingsStore(string filePath) => FilePath = filePath;

    /// <summary>Ruta por defecto: %LOCALAPPDATA%\Ritmo\settings.json.</summary>
    public static JsonSettingsStore Default()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ritmo");
        return new JsonSettingsStore(Path.Combine(dir, "settings.json"));
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return AppSettings.Default;

            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
                return AppSettings.Default;

            var dto = JsonSerializer.Deserialize<SettingsDto>(json, Options);
            return dto is null ? AppSettings.Default : SettingsMapper.FromDto(dto);
        }
        catch (JsonException)
        {
            // Archivo corrupto: no reventar, volver a valores por defecto.
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var dto = SettingsMapper.ToDto(settings);
        var json = JsonSerializer.Serialize(dto, Options);

        // Escritura atómica: a un temporal y luego reemplazo, para no dejar el
        // archivo a medias si algo falla a mitad de escritura.
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(FilePath))
            File.Replace(tmp, FilePath, null);
        else
            File.Move(tmp, FilePath);
    }
}
