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
    public static ISettingsStore Store { get; } = JsonSettingsStore.Default();
    public static ConfigurationService Config { get; } = new(Store);

    /// <summary>Carga el estado actual desde disco.</summary>
    public static AppSettings Load() => Store.Load();

    /// <summary>
    /// Asegura que hay algo que mostrar la primera vez: si no hay ninguna fase,
    /// siembra un horario de ejemplo (las fases TAI) para que el usuario vea la
    /// rejilla con contenido. No pisa nada si ya hay datos.
    /// </summary>
    public static void EnsureSeeded()
    {
        var s = Store.Load();
        if (s.Plan.Phases.Count > 0 || s.Schedule.Sessions.Count > 0) return;
        Store.Save(s with { Plan = SampleData.TaiPlan() });
    }
}
