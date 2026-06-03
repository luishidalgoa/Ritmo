using System;
using Ritmo.Core.Commands;
using Ritmo.Core.Persistence;

namespace Ritmo_App.Services;

/// <summary>
/// Acceso único al estado de la app (settings) para toda la UI. Comparte el
/// mismo JsonSettingsStore que usa el servidor MCP, así lo que configure la IA
/// o la UI se ve en ambos lados.
/// </summary>
public static class AppState
{
    // Store redirigible: notifica tras cada guardado (#128) y permite desviar todas las
    // lecturas/escrituras a una copia EN MEMORIA durante el "modo demo" del tutorial.
    private static readonly RedirectableStore _store = new(JsonSettingsStore.Default());

    public static ISettingsStore Store => _store;
    public static ConfigurationService Config { get; } = new(_store);

    /// <summary>
    /// Se dispara tras CADA guardado de ajustes (lo cause la UI o la IA por MCP). Lo usa
    /// el host del horario para re-planificar los avisos cuando cambia el horario. #128
    /// </summary>
    public static event Action? SettingsChanged
    {
        add => _store.Saved += value;
        remove => _store.Saved -= value;
    }

    /// <summary>Carga el estado actual desde disco.</summary>
    public static AppSettings Load() => _store.Load();

    /// <summary>
    /// ¿Es el primer arranque? (#83) Lo es mientras el usuario no haya completado el
    /// onboarding. Ya no se siembra ningún horario de ejemplo: en su lugar el onboarding
    /// deja elegir una plantilla de categorías neutra. La migración marca este flag a true
    /// para los usuarios EXISTENTES (que ya tienen datos), así no ven el onboarding.
    /// </summary>
    public static bool IsFirstRun() => !_store.Load().OnboardingCompleted;

    /// <summary>¿Estamos dentro del "modo demo" del tutorial? (nada se persiste a disco).</summary>
    public static bool IsDemo => _store.IsDemo;

    /// <summary>
    /// Entra en "modo demo": a partir de aquí TODA lectura/escritura va a una copia EN
    /// MEMORIA, sembrada con el estado actual. El settings.json del disco NO se toca. Lo
    /// usa el tutorial de primer arranque para que el usuario "monte" un horario de ejemplo
    /// sin ensuciar ni cambiar su configuración real. Idempotente.
    /// </summary>
    public static void BeginDemo()
    {
        if (_store.IsDemo) return;
        _store.EnterDemo(new InMemorySettingsStore(_store.Load()));
    }

    /// <summary>
    /// Sale del "modo demo". Si <paramref name="persist"/> es true, vuelca el estado de la
    /// demo al disco REAL (es el plan inicial del usuario nuevo). Si es false, lo DESCARTA y
    /// el disco queda EXACTAMENTE como estaba. En ambos casos dispara SettingsChanged para
    /// que la UI recargue del store real.
    /// </summary>
    public static void EndDemo(bool persist) => _store.ExitDemo(persist);

    /// <summary>
    /// Store que reenvía a un destino y notifica tras guardar (#128). El destino activo es
    /// el real (disco) salvo durante el modo demo, en el que apunta a uno en memoria.
    /// </summary>
    private sealed class RedirectableStore(ISettingsStore real) : ISettingsStore
    {
        private readonly ISettingsStore _real = real;
        private ISettingsStore? _demo;

        public event Action? Saved;

        public bool IsDemo => _demo is not null;
        private ISettingsStore Active => _demo ?? _real;

        public AppSettings Load() => Active.Load();

        public void Save(AppSettings settings)
        {
            Active.Save(settings);
            Saved?.Invoke();
        }

        public void EnterDemo(ISettingsStore demo)
        {
            _demo = demo;
            Saved?.Invoke();   // la UI recarga ya desde la copia en memoria
        }

        public void ExitDemo(bool persist)
        {
            if (_demo is not null && persist)
                _real.Save(_demo.Load());   // conservar: vuelca la demo al disco real
            _demo = null;
            Saved?.Invoke();                // la UI recarga desde el store real
        }
    }
}
