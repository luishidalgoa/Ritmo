using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Ritmo_App.Services;

namespace Ritmo_App;

/// <summary>
/// Guion del tutorial de primer arranque (#tutorial). Vive aquí (parcial de MainWindow) porque
/// necesita los controles reales (nav, panel de entornos, páginas, isla). Corre en MODO DEMO: todo
/// lo que crea el usuario va a una copia en memoria; su settings.json real NO se toca (ver
/// <see cref="Services.AppState.BeginDemo"/>). El motor genérico está en <see cref="TutorialController"/>.
/// </summary>
public sealed partial class MainWindow
{
    private const int TutorialTotal = 12;

    // Isla y notas DEMO (se abren en los pasos finales; el orquestador las cierra al terminar/abortar).
    private FocusOverlayWindow? _tutIsland;
    private IslandNotesWindow? _tutNotes;

    private static string TutStep(int n) => Loc.Pick($"Paso {n} de {TutorialTotal}", $"Step {n} of {TutorialTotal}");

    /// <summary>
    /// Recorte sobre un item del NavigationView que avanza cuando el usuario lo INVOCA (detección por
    /// <see cref="NavigationView.ItemInvoked"/> + Tag, más fiable que Tapped en un NavigationViewItem).
    /// </summary>
    private Task<bool> SpotlightNavAsync(TutorialController tut, NavigationViewItem target, string tag,
                                         string badge, string title, string body)
    {
        void OnInvoked(NavigationView s, NavigationViewItemInvokedEventArgs e)
        {
            if ((e.InvokedItemContainer?.Tag as string) == tag) tut.SignalAction();
        }
        return tut.SpotlightUntil(target, badge, title, body,
            subscribe: () => Nav.ItemInvoked += OnInvoked,
            unsubscribe: () => Nav.ItemInvoked -= OnInvoked);
    }

    /// <summary>Busca un item del NavigationView por su Tag (menú principal o footer).</summary>
    private NavigationViewItem? NavItemByTag(string tag)
    {
        foreach (var o in Nav.MenuItems)
            if (o is NavigationViewItem it && (it.Tag as string) == tag) return it;
        foreach (var o in Nav.FooterMenuItems)
            if (o is NavigationViewItem it && (it.Tag as string) == tag) return it;
        return null;
    }

    /// <summary>
    /// Ejecuta el guion completo. Devuelve true si el usuario lo COMPLETÓ (false si lo abandonó).
    /// No persiste nada por sí mismo: el orquestador decide al final si volcar la demo al disco y
    /// cierra la isla/notas demo.
    /// </summary>
    internal async Task<bool> RunTutorialFlowAsync(TutorialController tut)
    {
        // ---- P0 · Bienvenida ----
        if (!await tut.Message(TutStep(1),
            Loc.Pick("¡Bienvenido a Ritmo!", "Welcome to Ritmo!"),
            Loc.Pick("Vamos a montar tu primer horario en unos minutos. Te guío paso a paso; solo tendrás que pulsar lo que te señale.",
                     "Let's set up your first schedule in a few minutes. I'll guide you step by step; just click what I highlight."),
            nextLabel: Loc.Pick("Empezar", "Start")))
            return false;

        // ---- P1 · Abrir el panel de Entornos ----
        if (!await SpotlightNavAsync(tut, WorkEnvNav, "workenv", TutStep(2),
            Loc.Pick("Crea un entorno", "Create an environment"),
            Loc.Pick("Un entorno define qué pasa al concentrarte: No molestar, bloquear webs, escritorio virtual… Ábrelo aquí.",
                     "An environment defines what happens when you focus: Do Not Disturb, block sites, virtual desktop… Open it here.")))
            return false;

        // Asegurar el panel abierto y refrescar el recorte sobre el botón recién creado.
        if (!(RightPanel.IsPaneOpen && _panelMode == PanelMode.WorkEnv))
        {
            BuildWorkEnvPanel();
            RightPanel.IsPaneOpen = true;
        }
        tut.Reposition();

        // ---- P2 · Crear el entorno (gate: aparece un entorno) ----
        if (TutorialNewEnvBtn is { } newEnvBtn)
        {
            int envBefore = AppState.Load().FocusEnvironments.Count;
            void OnEnv() { if (AppState.Load().FocusEnvironments.Count > envBefore) tut.SignalAction(); }
            if (!await tut.SpotlightUntil(newEnvBtn, TutStep(3),
                Loc.Pick("Nuevo entorno", "New environment"),
                Loc.Pick("Pulsa «Nuevo entorno», ponle un nombre (p. ej. Estudio) y guarda.",
                         "Click “New environment”, give it a name (e.g. Study) and save."),
                subscribe: () => AppState.SettingsChanged += OnEnv,
                unsubscribe: () => AppState.SettingsChanged -= OnEnv))
                return false;
        }

        // ---- P3 · Apps y webs (opcional) ----
        if (!await tut.Message(TutStep(4),
            Loc.Pick("Apps y webs (opcional)", "Apps and sites (optional)"),
            Loc.Pick("Dentro de un entorno puedes bloquear webs que te distraen o cerrar apps al concentrarte. Puedes hacerlo ahora o más tarde.",
                     "Inside an environment you can block distracting sites or close apps when focusing. You can do it now or later."),
            optional: true))
            return false;

        // ---- P4 · Crear una fase (Ajustes › Fases; gate: el plan tiene una fase) ----
        if (!await SpotlightNavAsync(tut, NavItemByTag("settings")!, "settings", TutStep(5),
            Loc.Pick("Abre Ajustes", "Open Settings"),
            Loc.Pick("Las fases viven en Ajustes. Ábrelo para crear la primera.",
                     "Phases live in Settings. Open it to create the first one.")))
            return false;
        {
            int phaseBefore = AppState.Load().Plan.Phases.Count;
            void OnPhase() { if (AppState.Load().Plan.Phases.Count > phaseBefore) tut.SignalAction(); }
            if (!await tut.MessageUntil(TutStep(6),
                Loc.Pick("Crea una fase", "Create a phase"),
                Loc.Pick("En Ajustes › Fases, añade una fase: un tramo del curso con su horario (p. ej. «Trimestre 1»).",
                         "In Settings › Phases, add a phase: a stretch of the term with its schedule (e.g. “Term 1”)."),
                subscribe: () => AppState.SettingsChanged += OnPhase,
                unsubscribe: () => AppState.SettingsChanged -= OnPhase))
                return false;
        }

        // ---- P5 · Crear una sesión (Horario; gate: aumenta el nº de sesiones) ----
        if (!await SpotlightNavAsync(tut, NavItemByTag("schedule")!, "schedule", TutStep(7),
            Loc.Pick("Abre el Horario", "Open the Schedule"),
            Loc.Pick("Ahora colocamos tus bloques de estudio en el horario semanal.",
                     "Now let's place your study blocks on the weekly schedule.")))
            return false;
        {
            int before = SessionCount();
            void OnSession() { if (SessionCount() > before) tut.SignalAction(); }
            if (!await tut.MessageUntil(TutStep(8),
                Loc.Pick("Añade una sesión", "Add a session"),
                Loc.Pick("Pulsa «Añadir sesión», elige los días (L–V) y una hora (p. ej. 17:00–18:00) y guarda.",
                         "Click “Add session”, pick the days (Mon–Fri) and a time (e.g. 5–6pm) and save."),
                subscribe: () => AppState.SettingsChanged += OnSession,
                unsubscribe: () => AppState.SettingsChanged -= OnSession))
                return false;
        }

        // ---- P6 · Concentración: abrir la isla DEMO (sin aplicar tu entorno real) ----
        if (!await tut.Message(TutStep(9),
            Loc.Pick("Concentración", "Focus"),
            Loc.Pick("Cuando pulses «Iniciar» en el Temporizador entras en concentración y aparece la isla flotante. Te la enseño en demo, sin tocar tu equipo.",
                     "When you press “Start” on the Timer you enter focus and the floating island appears. Let me show it in demo mode, nothing on your PC changes."),
            nextLabel: Loc.Pick("Ver la isla", "Show the island")))
            return false;

        // Isla autónoma: NO pasa por el PomodoroEngine ni aplica el entorno (cero efectos reales).
        _tutIsland = new FocusOverlayWindow();
        _tutIsland.UpdateView(
            clock: DateTime.Now.ToString("HH:mm"),
            date: DateTime.Now.ToString("dddd d"),
            phaseLabel: Loc.Pick("CONCENTRACIÓN", "FOCUS"),
            pomo: "25:00",
            progress: 0.08, isRunning: true, canSkip: true, isBreak: false);
        _tutIsland.Activate();
        var island = _tutIsland;

        // ---- P7 · Tomar una nota desde la isla (gate: aparece una nota) ----
        {
            string demoTitle = AppState.Load().FocusEnvironments.FirstOrDefault()?.Name
                               ?? Loc.Pick("Estudio", "Study");
            int notesBefore = AppState.Load().Notes.Count;
            void OpenNotes()
            {
                if (_tutNotes is not null) { _tutNotes.Activate(); return; }
                _tutNotes = new IslandNotesWindow(demoTitle, null);
                _tutNotes.Closed += (_, _) => _tutNotes = null;
                _tutNotes.Activate();
            }
            void OnNote() { if (AppState.Load().Notes.Count > notesBefore) tut.SignalAction(); }

            if (!await tut.MessageUntil(TutStep(10),
                Loc.Pick("Toma una nota", "Take a note"),
                Loc.Pick("En la isla flotante pulsa el botón de notas, escribe algo y guárdalo. Tus notas te acompañan aunque estés concentrado.",
                         "On the floating island press the notes button, type something and save it. Your notes stay with you while you focus."),
                subscribe: () => { island.NotesRequested += OpenNotes; AppState.SettingsChanged += OnNote; },
                unsubscribe: () => { island.NotesRequested -= OpenNotes; AppState.SettingsChanged -= OnNote; },
                optional: true))
                return false;

            _tutNotes?.Close();
            _tutNotes = null;
        }

        // ---- P8 · Salir de la concentración (gate: pulsar el botón de salir de la isla) ----
        {
            void OnExit() => tut.SignalAction();
            if (!await tut.MessageUntil(TutStep(11),
                Loc.Pick("Salir de la concentración", "Leave focus"),
                Loc.Pick("Cuando termines, sal de la concentración con el botón de salir de la isla (vuelve a la app).",
                         "When you're done, leave focus with the island's exit button (it returns to the app)."),
                subscribe: () => island.ExpandRequested += OnExit,
                unsubscribe: () => island.ExpandRequested -= OnExit))
                return false;

            _tutIsland?.Close();
            _tutIsland = null;
        }

        // ---- P9 · Cierre ----
        if (!await tut.Message(TutStep(12),
            Loc.Pick("¡Listo!", "All set!"),
            Loc.Pick("Has creado tu primer horario: entorno, fase y sesiones, y ya sabes concentrarte y tomar notas. ¡A por ello!",
                     "You've created your first schedule: environment, phase and sessions, and you know how to focus and take notes. Go for it!"),
            nextLabel: Loc.Pick("Terminar", "Finish")))
            return false;

        return true;
    }

    /// <summary>
    /// Orquesta el tutorial de primer arranque (#tutorial). Abre el MODO DEMO (nada se persiste),
    /// siembra categorías neutras para que el editor funcione, corre el guion y, al terminar, ofrece
    /// CONSERVAR lo creado como plan inicial. Si el usuario abandona o no lo conserva, el disco queda
    /// intacto y se siembra la plantilla neutra + una fase vacía (como el onboarding clásico) para que
    /// la app sea usable y no se repita el tutorial.
    /// </summary>
    internal async Task RunTutorial(bool firstRun)
    {
        AppState.BeginDemo();
        // En primer arranque la config está vacía: sembrar categorías neutras para que el selector de
        // la sesión (KindBox) funcione. En replay (flag) NO se siembra: la demo ya clonó tus categorías.
        if (firstRun)
            AppState.Config.SeedTemplate(Ritmo.Core.Model.CategoryDefaults.Blank);

        var tut = new TutorialController(Tutorial);
        bool completed = false;
        try
        {
            completed = await RunTutorialFlowAsync(tut);
        }
        finally
        {
            tut.Finish();   // ocultar el overlay
            if (_tutNotes is not null) { var n = _tutNotes; _tutNotes = null; n.Close(); }
            if (_tutIsland is not null) { var i = _tutIsland; _tutIsland = null; i.Close(); }
        }

        if (!firstRun)
        {
            // Replay (flag de verificación): NUNCA persistir ni tocar el store real. Se descarta la demo.
            AppState.EndDemo(persist: false);
        }
        else if (completed && await AskKeepStarterPlan())
        {
            // Conservar: la demo (categorías + entorno + fase + sesiones + OnboardingCompleted) se vuelca al disco.
            AppState.EndDemo(persist: true);
        }
        else
        {
            // Descartar en primer arranque: dejar la app usable y NO repetir el tutorial (como el onboarding clásico).
            AppState.EndDemo(persist: false);
            AppState.Config.SeedTemplate(Ritmo.Core.Model.CategoryDefaults.Blank);
            if (AppState.Load().Plan.Phases.Count == 0)
                AppState.Config.AddPhase("Mi horario", DateOnly.FromDateTime(DateTime.Now), null);
        }

        RebuildEnvNavItems();
        ContentFrame.Navigate(typeof(HomePage));
    }

    /// <summary>Pregunta si quiere conservar el horario de ejemplo como plan inicial.</summary>
    private async Task<bool> AskKeepStarterPlan()
    {
        var dlg = new ContentDialog
        {
            XamlRoot = Nav.XamlRoot,
            Title = Loc.Pick("¿Guardar este horario?", "Save this schedule?"),
            Content = Loc.Pick(
                "Puedes quedarte con el entorno, la fase y las sesiones que acabas de crear como tu plan inicial, o empezar de cero.",
                "You can keep the environment, phase and sessions you just created as your starting plan, or start fresh."),
            PrimaryButtonText = Loc.Pick("Guardar plan", "Keep plan"),
            CloseButtonText = Loc.Pick("Empezar de cero", "Start fresh"),
            DefaultButton = ContentDialogButton.Primary
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>Nº total de sesiones recurrentes en todas las fases del plan (para el gate de P5).</summary>
    private static int SessionCount()
        => AppState.Load().Plan.Phases.Sum(p => p.Schedule.Sessions.Count);
}
