namespace Ritmo.Core.Model;

/// <summary>
/// Presets estándar de aviso previo que ofrece la UI (1 hora / 10 min / 5 min) y
/// helpers puros para componer la lista final de avisos de una sesión. La UI marca
/// casillas; aquí se decide la lista resultante, preservando avisos no estándar que
/// hayan podido entrar por otra vía (p. ej. la API/IA). Testable sin UI.
/// </summary>
public static class PreAlertPresets
{
    /// <summary>Minutos de los presets ofrecidos en la UI, de mayor a menor.</summary>
    public static readonly IReadOnlyList<int> Standard = [60, 10, 5];

    /// <summary>¿Es uno de los presets estándar?</summary>
    public static bool IsStandard(int minutes) => Standard.Contains(minutes);

    /// <summary>Avisos de la sesión que NO son presets (vinieron de la API/IA, etc.).</summary>
    public static IReadOnlyList<int> NonStandardOf(IEnumerable<PreAlert> alerts)
        => alerts.Select(a => a.MinutesBefore).Where(m => !IsStandard(m)).Distinct().ToList();

    /// <summary>
    /// Compone la lista final: presets seleccionados + avisos preservados (no estándar),
    /// sin duplicados y ordenada de mayor a menor (el aviso más lejano primero).
    /// </summary>
    public static IReadOnlyList<PreAlert> Compose(
        IEnumerable<int> selectedPresetMinutes,
        IEnumerable<int>? preservedNonStandard = null)
    {
        var all = selectedPresetMinutes
            .Concat(preservedNonStandard ?? [])
            .Where(m => m > 0)
            .Distinct()
            .OrderByDescending(m => m);
        return all.Select(m => new PreAlert(m)).ToList();
    }
}
