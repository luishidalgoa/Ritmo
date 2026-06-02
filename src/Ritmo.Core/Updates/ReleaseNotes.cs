using System.Collections.Generic;
using System.Linq;

namespace Ritmo.Core.Updates;

/// <summary>
/// Nota de versión a NIVEL USUARIO (no técnica): lo que se muestra en el carrusel
/// «Novedades» cuando la app se actualiza. Una entrada por versión publicada.
/// </summary>
public sealed record ReleaseNote
{
    /// <summary>Versión del paquete a la que corresponde (p. ej. "1.0.1.0").</summary>
    public required string Version { get; init; }
    /// <summary>Título amable de la diapositiva.</summary>
    public required string Title { get; init; }
    /// <summary>Emoji/ilustración de la diapositiva.</summary>
    public string Emoji { get; init; } = "✨";
    /// <summary>Puntos clave, en lenguaje de usuario.</summary>
    public IReadOnlyList<string> Highlights { get; init; } = [];
}

/// <summary>
/// Catálogo curado de novedades por versión + selección de "qué enseñar". PURO y
/// testeable. La regla del harness (ver AGENTS.md) mantiene esta lista al día: al
/// shippear una feature de cara al usuario, se añade un highlight a la versión actual.
/// </summary>
public static class ReleaseNotes
{
    /// <summary>Novedades por versión (la más reciente al final de la lista).</summary>
    public static IReadOnlyList<ReleaseNote> All { get; } =
    [
        new ReleaseNote
        {
            Version = "1.0.1.0",
            Title = "Tu horario, más tuyo",
            Emoji = "🗓️",
            Highlights =
            [
                "El horario se adapta al tamaño de la ventana y marca la hora actual de cada día.",
                "Coloca bloques a cualquier hora (p. ej. 16:40) y elige la granularidad de la rejilla (60/30/15 min).",
                "Personaliza el color de fondo de cada tipo de bloque desde Ajustes.",
                "Al empezar a concentrarte, una vista previa te recuerda los bloques de hoy.",
                "Recibe los avisos del horario también en el móvil (notificaciones por ntfy).",
            ]
        },
        new ReleaseNote
        {
            Version = "1.0.2.0",
            Title = "Ritmo, a tu manera",
            Emoji = "🧩",
            Highlights =
            [
                "Ahora Ritmo es tuyo: define tus propias categorías de bloque (nombre, color y si activan concentración) en Ajustes → Categorías.",
                "Al empezar, elige una plantilla — Estudio, Trabajo o Genérico — en vez de un horario fijo. Tus categorías de siempre se conservan.",
                "Cada entorno se despliega en módulos: Concentración, Enlaces, Tareas y Herramientas.",
                "Toca un módulo para editar solo esa parte, sin perderte en un formulario gigante.",
                "«Abrir workspace» lanza de golpe todos los enlaces del entorno en una ventana nueva del navegador.",
                "Cada entorno tiene su lista de Tareas: añade, marca como hecha y reordena.",
                "Marca apps como «Silenciar» y Ritmo las silencia al concentrarte (y las restaura al terminar).",
                "Los avisos del horario llegan al PC y al móvil de forma fiable, también en sesiones provisionales y al editar el horario.",
                "Opción para que Ritmo arranque solo al iniciar sesión en Windows, en segundo plano: tus avisos suenan sin tener que abrirlo.",
                "Icono en la bandeja del sistema: al cerrar la ventana Ritmo sigue activo para tus avisos; ábrelo o ciérralo del todo desde ahí.",
                "Elige el aviso previo por defecto de las sesiones nuevas desde Ajustes.",
                "Duplica una fase del horario para crear la siguiente a partir de ella, sin rehacerla.",
                "Copia y pega sesiones con Ctrl+C / Ctrl+V: se pegan donde tengas el ratón, si el hueco está libre.",
                "Las sesiones extraordinarias se ponen en fechas concretas (un día o un rango), con calendario.",
                "Próximamente: vincular tu calendario externo a cada entorno.",
            ]
        },
        new ReleaseNote
        {
            Version = "1.0.3.0",
            Title = "Detalles que se agradecen",
            Emoji = "✨",
            Highlights =
            [
                "Borra una sesión del calendario con la tecla Suprimir: selecciónala y pulsa Supr.",
                "Las sesiones que se solapan el mismo día ahora se ven lado a lado, no una encima de otra.",
                "Modo descanso: pausa los avisos con un interruptor, o programa periodos (vacaciones) de fecha a fecha.",
            ]
        },
        new ReleaseNote
        {
            Version = "1.0.4.0",
            Title = "Para quien factura por horas",
            Emoji = "💼",
            Highlights =
            [
                "Nueva sección «Trabajo»: lleva tus horas y ganancias por proyecto o cliente, con tarifa, objetivo y moneda.",
                "Gráficos por proyecto: barras de horas por día y línea de lo acumulado frente a tu objetivo del mes.",
                "Pensado para quien no tiene horario fijo: vas sumando horas día a día y ves la estimación de fin de mes.",
                "Vincula una categoría del horario a un proyecto y todas sus sesiones cuentan solas; marca un día como «no realizado» o «parcial» cuando falles o salgas antes.",
                "¿Un cambio sin querer en el horario? Deshaz con Ctrl+Z y rehaz con Ctrl+Y.",
                "Pasa el ratón por el «?» de los campos: ahora hay explicaciones con ejemplos en lo nuevo.",
                "Al concentrarte, Ritmo deja de parpadear en la barra de tareas para no distraerte.",
            ]
        },
        new ReleaseNote
        {
            Version = "1.1.0.0",
            Title = "Ritmo en tu idioma… y mucho más",
            Emoji = "🌍",
            Highlights =
            [
                "Ritmo ahora habla inglés: cámbialo en Ajustes → Apariencia → Idioma.",
                "Bloquea las webs que te distraen mientras te concentras (y, con la extensión de navegador, también a nivel de red).",
                "Sincroniza tus tareas con Google Tasks en ambos sentidos.",
                "Publica tu horario en Google Calendar y muestra tus eventos de Google en «Hoy».",
                "Isla flotante de concentración: arrástrala donde quieras, compáctala y abre las notas desde ella.",
                "Toma notas de la sesión sin salir de la concentración; ahora también puedes asignarlas a una categoría de bloque.",
                "Atajo a las notas desde el navbar, agrupadas por hoy / generales / otras.",
                "Ajustes reorganizados por temas (Tu plan · Concentración · Conexiones · Notas · La app), más fáciles de recorrer.",
                "Nueva pestaña «Hecho por» con quién hay detrás de Ritmo.",
            ]
        },
    ];

    /// <summary>Novedades en inglés (#48 i18n). Mismas versiones/emojis que <see cref="All"/>, traducidas.</summary>
    public static IReadOnlyList<ReleaseNote> AllEn { get; } =
    [
        new ReleaseNote
        {
            Version = "1.0.1.0",
            Title = "Your schedule, more yours",
            Emoji = "🗓️",
            Highlights =
            [
                "The schedule adapts to the window size and marks the current time of each day.",
                "Place blocks at any time (e.g. 16:40) and choose the grid granularity (60/30/15 min).",
                "Customize the background color of each block type from Settings.",
                "When you start focusing, a preview reminds you of today's blocks.",
                "Get schedule reminders on your phone too (ntfy notifications).",
            ]
        },
        new ReleaseNote
        {
            Version = "1.0.2.0",
            Title = "Ritmo, your way",
            Emoji = "🧩",
            Highlights =
            [
                "Now Ritmo is yours: define your own block categories (name, color, and whether they trigger focus) in Settings → Categories.",
                "When you start, pick a template — Study, Work, or Generic — instead of a fixed schedule. Your usual categories are kept.",
                "Each environment unfolds into modules: Focus, Links, Tasks, and Tools.",
                "Tap a module to edit just that part, without getting lost in a giant form.",
                "\"Open workspace\" launches all the environment's links at once in a new browser window.",
                "Each environment has its Task list: add, check off, and reorder.",
                "Mark apps as \"Mute\" and Ritmo silences them while you focus (and restores them when you finish).",
                "Schedule reminders reach your PC and phone reliably, including tentative sessions and when you edit the schedule.",
                "Option for Ritmo to start automatically when you sign in to Windows, in the background: your reminders fire without opening it.",
                "System tray icon: when you close the window Ritmo stays active for your reminders; open it or quit completely from there.",
                "Choose the default pre-alert for new sessions from Settings.",
                "Duplicate a schedule phase to build the next one from it, without redoing it.",
                "Copy and paste sessions with Ctrl+C / Ctrl+V: they paste where your mouse is, if the slot is free.",
                "Extraordinary sessions are placed on specific dates (a day or a range), with a calendar.",
                "Coming soon: link your external calendar to each environment.",
            ]
        },
        new ReleaseNote
        {
            Version = "1.0.3.0",
            Title = "Welcome little touches",
            Emoji = "✨",
            Highlights =
            [
                "Delete a session from the calendar with the Delete key: select it and press Del.",
                "Sessions that overlap on the same day now show side by side, not on top of each other.",
                "Rest mode: pause reminders with a switch, or schedule periods (holidays) from date to date.",
            ]
        },
        new ReleaseNote
        {
            Version = "1.0.4.0",
            Title = "For those who bill by the hour",
            Emoji = "💼",
            Highlights =
            [
                "New \"Work\" section: track your hours and earnings per project or client, with rate, goal, and currency.",
                "Per-project charts: daily hour bars and a line of the running total against your monthly goal.",
                "Designed for those without a fixed schedule: add hours day by day and see the end-of-month estimate.",
                "Link a schedule category to a project and all its sessions count automatically; mark a day as \"not done\" or \"partial\" when you miss it or leave early.",
                "An accidental change to the schedule? Undo with Ctrl+Z and redo with Ctrl+Y.",
                "Hover over the \"?\" on fields: there are now explanations with examples on the new stuff.",
                "While you focus, Ritmo stops flashing in the taskbar so it doesn't distract you.",
            ]
        },
        new ReleaseNote
        {
            Version = "1.1.0.0",
            Title = "Ritmo in your language… and much more",
            Emoji = "🌍",
            Highlights =
            [
                "Ritmo now speaks English: switch it in Settings → Appearance → Language.",
                "Block distracting sites while you focus (and, with the browser extension, at the network level too).",
                "Sync your tasks with Google Tasks both ways.",
                "Publish your schedule to Google Calendar and show your Google events in \"Today\".",
                "Floating focus island: drag it anywhere, make it compact, and open your notes from it.",
                "Take session notes without leaving focus; you can now also assign them to a block category.",
                "Notes shortcut from the navbar, grouped by today / general / other.",
                "Settings reorganized by topic (Your plan · Focus · Connections · Notes · The app), easier to skim.",
                "New \"Made by\" tab showing who's behind Ritmo.",
            ]
        },
    ];

    /// <summary>Catálogo de novedades en el idioma dado ("en" = inglés; cualquier otro = español).</summary>
    public static IReadOnlyList<ReleaseNote> For(string? lang) =>
        string.Equals(lang, "en", System.StringComparison.OrdinalIgnoreCase) ? AllEn : All;

    /// <summary>
    /// Notas con versión en el rango <c>(lastSeen, current]</c>, de la más nueva a la más antigua, en el
    /// idioma <paramref name="lang"/>. <paramref name="lastSeen"/> null (nunca vistas) = se trata como "0".
    /// </summary>
    public static IReadOnlyList<ReleaseNote> Since(string? lastSeen, string current, string? lang = null)
    {
        return For(lang)
            .Where(n => CompareVersions(n.Version, current) <= 0
                     && (string.IsNullOrWhiteSpace(lastSeen) || CompareVersions(n.Version, lastSeen) > 0))
            .OrderByDescending(n => n.Version, VersionComparer.Instance)
            .ToList();
    }

    /// <summary>
    /// Compara versiones de hasta 4 partes (major.minor.build.revision). Tolerante a
    /// formatos cortos o vacíos. &lt;0 si a&lt;b, 0 si iguales, &gt;0 si a&gt;b.
    /// </summary>
    public static int CompareVersions(string a, string b)
    {
        var pa = Parse(a);
        var pb = Parse(b);
        for (int i = 0; i < 4; i++)
        {
            int c = pa[i].CompareTo(pb[i]);
            if (c != 0) return c;
        }
        return 0;
    }

    private static int[] Parse(string? v)
    {
        var parts = new int[4];
        if (string.IsNullOrWhiteSpace(v)) return parts;
        var segs = v.Trim().Split('.');
        for (int i = 0; i < 4 && i < segs.Length; i++)
            int.TryParse(segs[i], out parts[i]);
        return parts;
    }

    private sealed class VersionComparer : IComparer<string>
    {
        public static readonly VersionComparer Instance = new();
        public int Compare(string? x, string? y) => CompareVersions(x ?? "", y ?? "");
    }
}
