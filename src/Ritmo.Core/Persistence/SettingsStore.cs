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
    public string FilePath { get; }

    public JsonSettingsStore(string filePath) => FilePath = filePath;

    /// <summary>
    /// Ruta por defecto: %USERPROFILE%\.ritmo\settings.json. Se usa esta y NO
    /// %LOCALAPPDATA% a propósito (#65): la app empaquetada (MSIX) redirige
    /// %LOCALAPPDATA% al LocalCache del paquete, mientras que el servidor MCP
    /// (proceso sin empaquetar) ve el AppData\Local real → acababan en archivos
    /// distintos y no compartían estado. %USERPROFILE%\.ritmo no sufre esa
    /// redirección, así que ambos procesos leen/escriben el MISMO archivo.
    /// </summary>
    public static JsonSettingsStore Default()
    {
        var shared = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ritmo", "settings.json");
        MigrateLegacyIfNeeded(shared);
        return new JsonSettingsStore(shared);
    }

    /// <summary>
    /// Si aún no existe el archivo compartido pero sí el antiguo (%LOCALAPPDATA%\
    /// Ritmo, posiblemente redirigido por MSIX), lo copia para no perder la
    /// configuración existente al cambiar de ubicación (#65). Best-effort.
    /// </summary>
    private static void MigrateLegacyIfNeeded(string sharedPath)
    {
        try
        {
            if (File.Exists(sharedPath)) return;
            var legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Ritmo", "settings.json");
            if (!File.Exists(legacy)) return;

            var dir = Path.GetDirectoryName(sharedPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(legacy, sharedPath);
        }
        catch { /* best-effort: si falla, se arranca con configuración nueva */ }
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return AppSettings.Default;

            var json = File.ReadAllText(FilePath);
            return SettingsJson.Deserialize(json);
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

        var json = SettingsJson.Serialize(settings);

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
