namespace Ritmo.Core.Focus;

/// <summary>Categoría de una app conocida (para agrupar en el selector).</summary>
public enum AppCategory { Productividad, Navegador, Mensajeria, Juegos, Musica, Otros }

/// <summary>
/// Una app conocida que suele distraer: su nombre visible, el nombre de proceso
/// (para cerrarla/silenciarla), su categoría, un término para detectar si está
/// instalada (en el registro) y una URL opcional para instalarla. #94
/// </summary>
public sealed record KnownApp(
    string Name,
    string ProcessName,
    AppCategory Category,
    string InstallUrl,
    string MatchTerm,
    string LaunchTarget = "");

/// <summary>
/// Catálogo curado de apps comunes por categoría. Datos puros (sin Windows) para
/// que el host detecte cuáles están instaladas y el usuario elija qué hacer con
/// cada una al concentrarse. #94
/// </summary>
public static class KnownApps
{
    public static readonly System.Collections.Generic.IReadOnlyList<KnownApp> Catalog =
    [
        // Productividad: herramientas de trabajo. LaunchTarget = protocolo o nombre en App Paths (#109).
        new("OneNote", "onenote", AppCategory.Productividad, "https://www.onenote.com/download", "onenote", "onenote:"),
        new("Microsoft Word", "WINWORD", AppCategory.Productividad, "https://www.microsoft.com/microsoft-365/word", "word", "winword"),
        new("Microsoft Excel", "EXCEL", AppCategory.Productividad, "https://www.microsoft.com/microsoft-365/excel", "excel", "excel"),
        new("Microsoft PowerPoint", "POWERPNT", AppCategory.Productividad, "https://www.microsoft.com/microsoft-365/powerpoint", "powerpoint", "powerpnt"),
        new("Microsoft Outlook", "OUTLOOK", AppCategory.Productividad, "https://www.microsoft.com/microsoft-365/outlook/email-and-calendar-software-microsoft-outlook", "outlook", "outlook"),
        new("Notion", "Notion", AppCategory.Productividad, "https://www.notion.so/desktop", "notion", "notion:"),
        new("Obsidian", "Obsidian", AppCategory.Productividad, "https://obsidian.md/download", "obsidian", "obsidian://"),
        new("Visual Studio Code", "Code", AppCategory.Productividad, "https://code.visualstudio.com/", "visual studio code", "code"),

        new("Microsoft Edge", "msedge", AppCategory.Navegador, "https://www.microsoft.com/edge", "microsoft edge", "msedge"),
        new("Google Chrome", "chrome", AppCategory.Navegador, "https://www.google.com/chrome/", "google chrome"),
        new("Mozilla Firefox", "firefox", AppCategory.Navegador, "https://www.mozilla.org/firefox/", "firefox"),
        new("Brave", "brave", AppCategory.Navegador, "https://brave.com/download/", "brave"),
        new("Opera", "opera", AppCategory.Navegador, "https://www.opera.com/", "opera"),

        new("Discord", "Discord", AppCategory.Mensajeria, "https://discord.com/download", "discord"),
        new("Slack", "slack", AppCategory.Mensajeria, "https://slack.com/downloads", "slack"),
        new("Telegram", "Telegram", AppCategory.Mensajeria, "https://telegram.org/", "telegram"),
        new("WhatsApp", "WhatsApp", AppCategory.Mensajeria, "https://www.whatsapp.com/download", "whatsapp"),
        new("Microsoft Teams", "ms-teams", AppCategory.Mensajeria, "https://www.microsoft.com/microsoft-teams/download-app", "teams"),

        new("Steam", "steam", AppCategory.Juegos, "https://store.steampowered.com/about/", "steam"),
        new("Epic Games", "EpicGamesLauncher", AppCategory.Juegos, "https://store.epicgames.com/", "epic games"),
        new("Battle.net", "Battle.net", AppCategory.Juegos, "https://www.blizzard.com/apps/battle.net/desktop", "battle.net"),

        new("Spotify", "Spotify", AppCategory.Musica, "https://www.spotify.com/download", "spotify", "spotify:"),
        // Navidrome (servidor propio) se integra aparte vía API Subsonic, no como app de proceso (#107).
    ];

    /// <summary>Etiqueta legible de una categoría.</summary>
    public static string Label(AppCategory c) => c switch
    {
        AppCategory.Productividad => "Productividad",
        AppCategory.Navegador => "Navegadores",
        AppCategory.Mensajeria => "Mensajería",
        AppCategory.Juegos => "Juegos",
        AppCategory.Musica => "Música",
        _ => "Otros"
    };

    /// <summary>El catálogo agrupado por categoría (en el orden del enum).</summary>
    public static System.Collections.Generic.IReadOnlyList<(AppCategory Category, System.Collections.Generic.IReadOnlyList<KnownApp> Apps)> ByCategory()
        => System.Linq.Enumerable.ToList(
               System.Linq.Enumerable.Select(
                   System.Linq.Enumerable.GroupBy(Catalog, a => a.Category),
                   g => (g.Key, (System.Collections.Generic.IReadOnlyList<KnownApp>)System.Linq.Enumerable.ToList(g))));

    /// <summary>Busca una app del catálogo por nombre de proceso (sin distinguir mayúsculas).</summary>
    public static KnownApp? ByProcess(string processName)
        => System.Linq.Enumerable.FirstOrDefault(Catalog,
               a => string.Equals(a.ProcessName, processName, System.StringComparison.OrdinalIgnoreCase));
}
