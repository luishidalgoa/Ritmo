using System;
using System.Linq;
using Ritmo.Core.Background;
using Ritmo.Core.Model;
using Ritmo.Core.Notifications;
using Ritmo.Core.Scheduling;
using Ritmo.Core.Timing;

namespace Ritmo_App.Services;

/// <summary>
/// Mantiene vivo el <see cref="ScheduleRunner"/> del núcleo durante la vida de la
/// app. Cuando llega un evento del horario (aviso previo o inicio de sesión),
/// muestra un toast de Windows con el texto que decide el núcleo.
///
/// El runner es puro y ya está testeado; aquí solo le damos reloj/scheduler reales
/// y conectamos su salida con el SO. Best-effort: si algo falla, no rompe la UI.
/// </summary>
public sealed class ScheduleHost : IDisposable
{
    public static ScheduleHost Instance { get; } = new();

    private readonly IClock _clock = new SystemClock();
    private readonly IScheduler _scheduler = new SystemScheduler();
    private ScheduleRunner? _runner;

    private ScheduleHost() { }

    /// <summary>(Re)arranca el host leyendo el horario persistido vigente.</summary>
    public void Start()
    {
        Stop();
        try
        {
            var schedule = ResolveActiveSchedule();
            if (schedule.Sessions.Count == 0) return;   // nada que vigilar todavía

            ToastService.EnsureRegistered();
            _runner = new ScheduleRunner(schedule, _clock, _scheduler);
            _runner.EventDue += OnEventDue;
            _runner.Start();
        }
        catch { /* tolerar: la app sigue funcionando sin avisos */ }
    }

    /// <summary>
    /// Mismo criterio que la pantalla de horario: fase activa hoy, si no la
    /// primera fase del plan, y como último recurso el horario suelto.
    /// </summary>
    private static WeeklySchedule ResolveActiveSchedule()
    {
        AppState.EnsureSeeded();
        var settings = AppState.Load();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var phase = settings.Plan.GetActivePhase(today)
                    ?? settings.Plan.OrderedPhases.FirstOrDefault();
        return phase?.Schedule ?? settings.Schedule;
    }

    private static void OnEventDue(PlannedEvent ev)
        => ToastService.Show(NotificationBuilder.ForEvent(ev));

    public void Stop()
    {
        if (_runner is null) return;
        _runner.EventDue -= OnEventDue;
        _runner.Dispose();
        _runner = null;
    }

    public void Dispose() => Stop();
}
