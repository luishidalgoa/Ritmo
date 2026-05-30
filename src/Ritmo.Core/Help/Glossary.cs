namespace Ritmo.Core.Help;

/// <summary>Una entrada de la enciclopedia/ayuda: término + explicación breve.</summary>
public sealed record GlossaryEntry(string Key, string Term, string Description);

/// <summary>
/// Glosario de conceptos de Ritmo. Fuente única de verdad para los tooltips de
/// ayuda y para la página de Ayuda/wiki, de modo que la explicación de un término
/// sea siempre la misma. Puro y testable.
/// </summary>
public static class Glossary
{
    public static readonly System.Collections.Generic.IReadOnlyList<GlossaryEntry> Entries =
    [
        new("pomodoro", "Pomodoro",
            "Técnica de estudio por intervalos: bloques de concentración seguidos de descansos cortos, " +
            "y un descanso largo cada varios focos. Ayuda a mantener la atención y a descansar a tiempo."),
        new("deep-work", "Ritmo profundo (50/10/20)",
            "Preset de Pomodoro con bloques largos: 50 min de concentración, 10 de descanso corto y " +
            "20 de descanso largo cada 2 focos. Encaja con sesiones de unas 2 horas."),
        new("classic", "Ritmo clásico (25/5/15)",
            "Preset de Pomodoro tradicional: 25 min de concentración, 5 de descanso corto y 15 de " +
            "descanso largo cada 4 focos. Bueno para tareas cortas o para arrancar."),
        new("rhythm", "Ritmo Pomodoro",
            "Un conjunto de duraciones con nombre (concentración, descansos y cada cuántos focos toca el " +
            "largo). Además de los de por defecto (Clásico, Profundo), puedes crear los tuyos en Ajustes y " +
            "elegirlos al configurar un entorno."),
        new("prealert", "Avisos previos",
            "Recordatorios antes de que empiece una sesión (p. ej. 10 minutos antes). Suenan como " +
            "notificación de Windows aunque la ventana esté cerrada."),
        new("environment", "Entorno de trabajo",
            "Un contexto reutilizable (p. ej. «Oposiciones» o «Proyecto X») con su música, apps a cerrar, " +
            "No molestar, enlaces y tareas. Al concentrarte en un bloque se aplica el entorno de su tipo."),
        new("dnd", "No molestar",
            "Silencia las notificaciones de Windows mientras dura la concentración y las restaura al terminar."),
        new("phase", "Fase",
            "Un tramo del plan con sus fechas (p. ej. «Fase 1», del 1 jun al 31 oct) y su propio horario semanal. " +
            "Permite cambiar el horario según la época."),
        new("session", "Sesión (bloque)",
            "Una franja del horario semanal: día, hora de inicio y fin, tipo y avisos previos. Arrástrala " +
            "para moverla o estira sus bordes para redimensionarla."),
        new("tentative", "Provisional",
            "Un hueco reservado para estudiar pero sin materia decidida todavía. Se ve atenuado y NO dispara " +
            "la concentración automáticamente (sus avisos sí pueden sonar)."),
        new("focus", "Concentración",
            "El modo de trabajo enfocado: arranca el temporizador del bloque actual y aplica su entorno " +
            "(música, cerrar apps, No molestar, abrir tus enlaces)."),
        new("background", "Segundo plano",
            "Al cerrar la ventana, Ritmo sigue vivo en segundo plano para que los avisos suenen igualmente. " +
            "Se sale del todo con «Salir de Ritmo» en Ajustes."),
    ];

    /// <summary>Busca una entrada por su clave (o null si no existe).</summary>
    public static GlossaryEntry? Find(string key)
        => System.Linq.Enumerable.FirstOrDefault(Entries, e => e.Key == key);
}
