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
    // Decorador que avisa tras cada guardado (#128): re-planificar avisos, refrescos…
    private static readonly ChangeNotifyingStore _store = new(JsonSettingsStore.Default());

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

    /// <summary>Store que reenvía a otro y notifica tras guardar (patrón decorador). #128</summary>
    private sealed class ChangeNotifyingStore(ISettingsStore inner) : ISettingsStore
    {
        public event Action? Saved;
        public AppSettings Load() => inner.Load();
        public void Save(AppSettings settings)
        {
            inner.Save(settings);
            Saved?.Invoke();
        }
    }
}
