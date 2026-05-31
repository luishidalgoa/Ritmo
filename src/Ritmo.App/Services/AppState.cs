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
    /// Asegura que hay algo que mostrar la primera vez: si no hay ninguna fase,
    /// siembra un horario de ejemplo (las fases TAI) para que el usuario vea la
    /// rejilla con contenido. No pisa nada si ya hay datos.
    /// </summary>
    public static void EnsureSeeded()
    {
        var s = _store.Load();
        if (s.Plan.Phases.Count > 0 || s.Schedule.Sessions.Count > 0) return;
        _store.Save(s with { Plan = SampleData.TaiPlan() });
    }

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
