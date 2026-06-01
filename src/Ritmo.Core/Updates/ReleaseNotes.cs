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
    ];

    /// <summary>
    /// Notas con versión en el rango <c>(lastSeen, current]</c>, de la más nueva a la
    /// más antigua. <paramref name="lastSeen"/> null (nunca vistas) = se trata como "0".
    /// </summary>
    public static IReadOnlyList<ReleaseNote> Since(string? lastSeen, string current)
    {
        return All
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
