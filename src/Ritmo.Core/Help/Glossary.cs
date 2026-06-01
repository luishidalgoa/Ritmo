namespace Ritmo.Core.Help;

/// <summary>
/// Una entrada de la enciclopedia/ayuda: término + explicación + (opcional) un ejemplo concreto.
/// <see cref="Example"/> se destaca en el tooltip para los conceptos menos intuitivos.
/// </summary>
public sealed record GlossaryEntry(string Key, string Term, string Description, string? Example = null);

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
            "Técnica de concentración por intervalos: bloques de concentración seguidos de descansos cortos, " +
            "y un descanso largo cada varios focos. Ayuda a mantener la atención y a descansar a tiempo.",
            "25 min de foco + 5 de descanso, y uno largo de 15 cada 4 focos."),
        new("deep-work", "Ritmo profundo (50/10/20)",
            "Preset de Pomodoro con bloques largos: 50 min de concentración, 10 de descanso corto y " +
            "20 de descanso largo cada 2 focos. Encaja con sesiones de unas 2 horas."),
        new("classic", "Ritmo clásico (25/5/15)",
            "Preset de Pomodoro tradicional: 25 min de concentración, 5 de descanso corto y 15 de " +
            "descanso largo cada 4 focos. Bueno para tareas cortas o para arrancar."),
        new("rhythm", "Ritmo Pomodoro",
            "Un conjunto de duraciones con nombre (concentración, descansos y cada cuántos focos toca el " +
            "largo). Además de los de por defecto (Clásico, Profundo), puedes crear los tuyos en Ajustes y " +
            "elegirlos al configurar un entorno.",
            "«Ritmo de tarde»: 45/10/30, largo cada 3."),
        new("prealert", "Avisos previos",
            "Recordatorios antes de que empiece una sesión. Suenan como notificación de Windows (y, si lo " +
            "activas, también en el móvil por ntfy) aunque la ventana esté cerrada. Puedes poner hasta dos.",
            "«10 minutos antes» de un bloque de las 10:00 te avisa a las 9:50."),
        new("environment", "Entorno de trabajo",
            "Un contexto reutilizable con su música, apps a cerrar, No molestar, enlaces y tareas. Al " +
            "concentrarte en un bloque se aplica el entorno asociado a su categoría.",
            "«Proyecto X»: abre el repo y el tablero, silencia Discord y pone música."),
        new("dnd", "No molestar",
            "Silencia las notificaciones de Windows mientras dura la concentración y las restaura al terminar."),
        new("phase", "Fase",
            "Un tramo del plan con sus fechas y su propio horario semanal. Permite cambiar el horario según " +
            "la época; al pasar la fecha límite entra la siguiente fase.",
            "«Fase 1» del 1 jun al 31 oct con un horario, «Fase 2» a partir del 1 nov con otro."),
        new("session", "Sesión (bloque)",
            "Una franja del horario semanal: día, hora de inicio y fin, categoría y avisos previos. Arrástrala " +
            "para moverla, estira sus bordes para redimensionarla, o pulsa Supr para borrarla."),
        new("tentative", "Provisional (no dispara concentración)",
            "Marca un bloque como reservado pero SIN contenido decidido: se ve atenuado y NO arranca la " +
            "concentración automáticamente al llegar su hora (sus avisos previos sí pueden sonar).",
            "Reservas 17:00–19:00 para «estudiar algo», pero aún no decides qué."),
        new("focus", "Concentración",
            "El modo de trabajo enfocado: arranca el temporizador del bloque actual y aplica su entorno " +
            "(música, cerrar apps, No molestar, abrir tus enlaces)."),
        new("background", "Segundo plano",
            "Al cerrar la ventana, Ritmo sigue vivo en segundo plano (con icono en la bandeja del sistema) " +
            "para que los avisos suenen igualmente. Se sale del todo con «Salir de Ritmo» (bandeja o Ajustes)."),

        // ---- Conceptos nuevos: categorías (#83), aviso por defecto (#48), descanso (#135) ----
        new("category", "Categoría",
            "La etiqueta de un bloque del horario, definida por ti: nombre, color y si activa la " +
            "concentración. Sustituye a los antiguos tipos fijos, para que cada persona (estudiante, " +
            "trabajador, freelance…) defina las suyas. Se gestionan en Ajustes → Categorías.",
            "«Reunión» (azul, activa concentración) o «Comida» (naranja, no la activa)."),
        new("focus-category", "Es de concentración",
            "Si está activado, al EMPEZAR un bloque de esta categoría Ritmo entra en modo concentración: " +
            "arranca el Pomodoro y aplica su entorno (música, cerrar apps, No molestar…). Las que no lo " +
            "tienen solo se muestran en el horario, sin disparar nada.",
            "«Estudio» o «Reunión» → sí. «Descanso» o «Comida» → no."),
        new("default-prealert", "Aviso previo por defecto",
            "Con cuánta antelación se PRE-RELLENA el aviso de una sesión NUEVA. Es solo el valor inicial: " +
            "puedes cambiarlo al crear cada sesión. No afecta a las sesiones ya creadas.",
            "Si lo pones en «10 minutos antes», cada bloque nuevo nacerá con ese aviso."),
        new("oneoff", "Sesión extraordinaria (en fechas concretas)",
            "Un bloque que NO se repite cada semana, sino en fechas concretas. Eliges «Desde» y «Hasta»: " +
            "la misma fecha en ambas = un solo día; un rango = se crea en cada día del rango a la misma hora.",
            "Un curso del 3 al 5 de junio, de 16:00 a 18:00 → tres bloques, uno por día."),
        new("rest-mode", "Modo descanso",
            "Pausa los avisos del horario SIN borrar nada (el horario se sigue viendo). Útil para vacaciones " +
            "o una pausa. Puedes activarlo manualmente «ahora» o programar periodos por fechas.",
            "De vacaciones: actívalo y no sonará ningún aviso hasta que lo apagues."),
        new("rest-period", "Periodo de descanso",
            "Un rango de fechas en el que el horario no lanza avisos (p. ej. vacaciones). Se activa solo en " +
            "esas fechas y vuelve a la normalidad al pasar.",
            "«Vacaciones de verano», del 1 al 31 de agosto."),

        // ---- Seguimiento laboral (#84 / #137) ----
        new("work-tracking", "Seguimiento laboral",
            "Lleva las horas que trabajas en un proyecto o cliente y cuánto ganas. Pensado para perfiles SIN " +
            "horario fijo: vas anotando horas día a día (o se computan solas desde el horario) y Ritmo calcula " +
            "el total del mes, lo ganado y una proyección de fin de mes.",
            "Freelance a 20 €/h: anotas 6 h hoy → +120 € este mes."),
        new("work-project", "Proyecto / cliente",
            "Un trabajo del que llevas las horas y las ganancias, con su tarifa, objetivo y color. Es " +
            "independiente de los entornos de concentración (un proyecto puede no tener nada que ver con tu foco).",
            "«Heladería», «Cliente A», «App de Juan»."),
        new("work-rate", "Tarifa por hora",
            "Lo que cobras por hora en este proyecto. Se usa para calcular cuánto llevas ganado a partir de " +
            "las horas registradas. Déjalo en 0 si solo quieres contar horas, sin dinero.",
            "25 €/h → 6 h trabajadas = 150 €."),
        new("work-goal", "Objetivo (h/mes)",
            "Tu meta de horas al mes en este proyecto. Ritmo muestra el % de progreso y dibuja la línea de " +
            "objetivo en el gráfico. Déjalo en 0 si no quieres objetivo.",
            "120 h/mes: si llevas 60, vas al 50%."),
        new("work-auto", "Computar horas desde el horario",
            "Si está activo, las sesiones del horario VINCULADAS a este proyecto suman sus horas solas los " +
            "días que tocan, sin que anotes nada. Si lo apagas, solo cuentan las horas que anotes a mano.",
            "Vinculas tu turno de 4 h de los lunes → cada lunes suma 4 h automáticamente."),
        new("work-link", "Proyecto (vínculo de la sesión)",
            "Vincula esta sesión del horario a un proyecto de seguimiento laboral. Si el proyecto computa " +
            "desde el horario, las horas de esta sesión se contarán solas los días que toca.",
            "El bloque «Turno tarde» vinculado a «Heladería»."),
        new("session-exception", "No realizada / parcial",
            "Marca que una sesión NO se hizo, o se hizo solo en parte, un día o un rango concreto. «No " +
            "realizada» no computa horas y se ve tachada; «parcial» computa solo las horas reales que indiques.",
            "Hoy salí 2 h antes → marca «parcial, 2 h». Festivo → «no realizada»."),
    ];

    /// <summary>Busca una entrada por su clave (o null si no existe).</summary>
    public static GlossaryEntry? Find(string key)
        => System.Linq.Enumerable.FirstOrDefault(Entries, e => e.Key == key);
}
