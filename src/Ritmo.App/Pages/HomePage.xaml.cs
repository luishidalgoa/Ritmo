using System;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Model;
using Ritmo.Core.Notifications;
using Ritmo.Core.Scheduling;
using Ritmo_App.Services;

namespace Ritmo_App;

/// <summary>
/// Vista "Hoy / Ahora" (#68): la superficie de inicio que conecta el plan con la
/// concentración. Muestra el bloque actual (y permite empezar ya), el siguiente
/// bloque del día y el próximo aviso programado. Es la primera pantalla de la app.
/// </summary>
public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        Loaded += (_, _) => Build();
    }

    private void Build()
    {
        var settings = AppState.Load();
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);

        DateText.Text = Capitalize(now.ToString("dddd d 'de' MMMM", new CultureInfo("es-ES")));

        var phase = settings.Plan.GetActivePhase(today) ?? settings.Plan.OrderedPhases.FirstOrDefault();
        var schedule = phase?.Schedule ?? settings.Schedule;
        var planner = new SchedulePlanner(schedule);

        // AHORA
        var active = planner.GetActiveSession(now);
        if (active is not null)
        {
            NowTitle.Text = active.Title;
            NowMeta.Text = $"{active.Kind.Label()} · {active.Start:HH\\:mm}–{active.End:HH\\:mm}";
            StartBtnText.Text = "Empezar concentración";
        }
        else
        {
            NowTitle.Text = "Sin bloque ahora";
            NowMeta.Text = "Puedes hacer una sesión de concentración libre";
            StartBtnText.Text = "Concentración libre";
        }

        // DESPUÉS (siguiente del día)
        var next = planner.GetNextSessionToday(now);
        if (next is not null)
        {
            NextTitle.Text = next.Title;
            NextMeta.Text = $"{next.Start:HH\\:mm} · {next.Kind.Label()}";
        }
        else
        {
            NextTitle.Text = "Nada más hoy";
            NextMeta.Text = "Has cubierto el día";
        }

        // PRÓXIMO AVISO (reflejo en-app de los toasts)
        var ev = planner.GetNextEvent(now);
        if (ev is not null)
        {
            var msg = NotificationBuilder.ForEvent(ev);
            AlertTitle.Text = msg.Title;
            AlertMeta.Text = $"{ev.At:HH\\:mm} · {ev.Session.Title}";
        }
        else
        {
            AlertTitle.Text = "Sin avisos próximos";
            AlertMeta.Text = "No hay nada programado por delante";
        }
    }

    private void StartBtn_Click(object sender, RoutedEventArgs e)
        => Navigator.GoToTimer(this, autoStart: true);

    private static string Capitalize(string s)
        => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}
