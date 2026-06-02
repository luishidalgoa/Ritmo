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

    /// <summary>Glosario en inglés (#48 i18n). Mismas claves que <see cref="Entries"/>, traducidas.</summary>
    public static readonly System.Collections.Generic.IReadOnlyList<GlossaryEntry> EntriesEn =
    [
        new("pomodoro", "Pomodoro",
            "Focus technique using intervals: focus blocks followed by short breaks, and a long break every " +
            "few focuses. Helps you keep your attention and rest in time.",
            "25 min focus + 5 break, and a long 15-min one every 4 focuses."),
        new("deep-work", "Deep rhythm (50/10/20)",
            "Pomodoro preset with long blocks: 50 min focus, 10 short break and 20 long break every 2 focuses. " +
            "Fits sessions of about 2 hours."),
        new("classic", "Classic rhythm (25/5/15)",
            "Traditional Pomodoro preset: 25 min focus, 5 short break and 15 long break every 4 focuses. " +
            "Good for short tasks or to get started."),
        new("rhythm", "Pomodoro rhythm",
            "A named set of durations (focus, breaks and how many focuses before the long one). Besides the " +
            "defaults (Classic, Deep), you can create your own in Settings and choose them when setting up an environment.",
            "\"Afternoon rhythm\": 45/10/30, long every 3."),
        new("prealert", "Pre-alerts",
            "Reminders before a session starts. They sound as a Windows notification (and, if enabled, on your " +
            "phone via ntfy) even when the window is closed. You can set up to two.",
            "\"10 minutes before\" a 10:00 block alerts you at 9:50."),
        new("environment", "Work environment",
            "A reusable context with its music, apps to close, Do Not Disturb, links and tasks. When you focus " +
            "on a block, the environment linked to its category is applied.",
            "\"Project X\": opens the repo and the board, mutes Discord and plays music."),
        new("dnd", "Do Not Disturb",
            "Silences Windows notifications during focus and restores them when you finish."),
        new("phase", "Phase",
            "A stretch of the plan with its dates and its own weekly schedule. Lets you change the schedule by " +
            "season; when the deadline passes, the next phase begins.",
            "\"Phase 1\" from Jun 1 to Oct 31 with one schedule, \"Phase 2\" from Nov 1 with another."),
        new("session", "Session (block)",
            "A slot of the weekly schedule: day, start and end time, category and pre-alerts. Drag it to move " +
            "it, stretch its edges to resize it, or press Del to delete it."),
        new("tentative", "Tentative (doesn't trigger focus)",
            "Marks a block as reserved but WITHOUT decided content: it looks dimmed and does NOT start focus " +
            "automatically when its time comes (its pre-alerts can still sound).",
            "You reserve 17:00–19:00 to \"study something\", but haven't decided what yet."),
        new("focus", "Focus",
            "The focused work mode: it starts the current block's timer and applies its environment (music, " +
            "close apps, Do Not Disturb, open your links)."),
        new("background", "Background",
            "When you close the window, Ritmo stays alive in the background (with a system tray icon) so " +
            "reminders still sound. Quit completely with \"Quit Ritmo\" (tray or Settings)."),
        new("category", "Category",
            "A schedule block's label, defined by you: name, color and whether it triggers focus. Replaces the " +
            "old fixed types, so everyone (student, worker, freelancer…) defines their own. Managed in Settings → Categories.",
            "\"Meeting\" (blue, triggers focus) or \"Lunch\" (orange, doesn't)."),
        new("focus-category", "Triggers focus",
            "If enabled, when a block of this category STARTS, Ritmo enters focus mode: it starts the Pomodoro " +
            "and applies its environment (music, close apps, Do Not Disturb…). Those without it just show on " +
            "the schedule, triggering nothing.",
            "\"Study\" or \"Meeting\" → yes. \"Break\" or \"Lunch\" → no."),
        new("default-prealert", "Default pre-alert",
            "How far in advance a NEW session's alert is PRE-FILLED. It's just the initial value: you can change " +
            "it when creating each session. It doesn't affect existing sessions.",
            "If you set \"10 minutes before\", each new block is born with that alert."),
        new("oneoff", "Extraordinary session (on specific dates)",
            "A block that does NOT repeat every week, but on specific dates. You choose \"From\" and \"To\": the " +
            "same date in both = a single day; a range = it's created on each day of the range at the same time.",
            "A course from June 3 to 5, 16:00 to 18:00 → three blocks, one per day."),
        new("rest-mode", "Rest mode",
            "Pauses the schedule's reminders WITHOUT deleting anything (the schedule is still visible). Useful " +
            "for holidays or a break. You can turn it on manually \"now\" or schedule periods by dates.",
            "On holiday: turn it on and no reminder will sound until you turn it off."),
        new("rest-period", "Rest period",
            "A date range where the schedule fires no reminders (e.g. holidays). It activates only on those " +
            "dates and returns to normal afterward.",
            "\"Summer holidays\", from August 1 to 31."),
        new("work-tracking", "Work tracking",
            "Tracks the hours you work on a project or client and how much you earn. Designed for profiles " +
            "WITHOUT a fixed schedule: you log hours day by day (or they're computed from the schedule) and " +
            "Ritmo calculates the month's total, earnings and an end-of-month projection.",
            "Freelancer at €20/h: log 6 h today → +€120 this month."),
        new("work-project", "Project / client",
            "A job whose hours and earnings you track, with its rate, goal and color. It's independent of focus " +
            "environments (a project may have nothing to do with your focus).",
            "\"Ice cream shop\", \"Client A\", \"Juan's app\"."),
        new("work-rate", "Hourly rate",
            "What you charge per hour on this project. Used to calculate how much you've earned from the logged " +
            "hours. Leave it at 0 if you only want to count hours, no money.",
            "€25/h → 6 h worked = €150."),
        new("work-goal", "Goal (h/month)",
            "Your monthly hours target for this project. Ritmo shows the % progress and draws the goal line on " +
            "the chart. Leave it at 0 if you don't want a goal.",
            "120 h/month: if you've done 60, you're at 50%."),
        new("work-auto", "Compute hours from the schedule",
            "If enabled, schedule sessions LINKED to this project add their hours automatically on the days they " +
            "fall, without you logging anything. If you turn it off, only the hours you log manually count.",
            "You link your 4-h Monday shift → each Monday adds 4 h automatically."),
        new("work-link", "Project (session link)",
            "Links this schedule session to a work-tracking project. If the project computes from the schedule, " +
            "this session's hours are counted automatically on the days it falls.",
            "The \"Evening shift\" block linked to \"Ice cream shop\"."),
        new("session-exception", "Not done / partial",
            "Marks that a session was NOT done, or done only partially, on a specific day or range. \"Not done\" " +
            "counts no hours and shows struck through; \"partial\" counts only the real hours you indicate.",
            "Today I left 2 h early → mark \"partial, 2 h\". Holiday → \"not done\"."),
    ];

    /// <summary>Glosario en el idioma dado ("en" = inglés; cualquier otro = español).</summary>
    public static System.Collections.Generic.IReadOnlyList<GlossaryEntry> For(string? lang) =>
        string.Equals(lang, "en", System.StringComparison.OrdinalIgnoreCase) ? EntriesEn : Entries;

    /// <summary>Busca una entrada por su clave en español (o null si no existe).</summary>
    public static GlossaryEntry? Find(string key)
        => System.Linq.Enumerable.FirstOrDefault(Entries, e => e.Key == key);

    /// <summary>Busca una entrada por su clave en el idioma dado (o null si no existe). #48</summary>
    public static GlossaryEntry? Find(string key, string? lang)
        => System.Linq.Enumerable.FirstOrDefault(For(lang), e => e.Key == key);
}
