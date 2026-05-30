using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Interop;
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

        // Entorno activo (#104): chip informativo de cuál se usará en la concentración libre.
        var activeEnv = settings.FocusEnvironments.FirstOrDefault(e => e.Id == settings.DefaultFocusEnvironmentId);
        if (activeEnv is not null)
        {
            ActiveEnvText.Text = $"Entorno: {activeEnv.Name}";
            ActiveEnvChip.Visibility = Visibility.Visible;
        }
        else ActiveEnvChip.Visibility = Visibility.Collapsed;

        BuildShortcuts(settings);
        BuildNotes(settings);
        _ = LoadCalendarAsync(settings);
    }

    /// <summary>Descarga y muestra los eventos de HOY de los calendarios suscritos (#112).</summary>
    private async System.Threading.Tasks.Task LoadCalendarAsync(Ritmo.Core.Persistence.AppSettings settings)
    {
        if (settings.CalendarFeeds.Count == 0) { CalendarSection.Visibility = Visibility.Collapsed; return; }
        var today = DateOnly.FromDateTime(DateTime.Now);
        IReadOnlyList<CalendarEvent> events;
        try { events = await CalendarService.FetchAsync(settings.CalendarFeeds, today, today); }
        catch { return; }

        CalendarPanel.Children.Clear();
        if (events.Count == 0)
        {
            CalendarPanel.Children.Add(new TextBlock { Text = "Sin eventos hoy.", Opacity = 0.6, FontSize = 13 });
        }
        else
        {
            foreach (var ev in events)
            {
                var time = ev.AllDay ? "Todo el día" : $"{ev.Start:HH\\:mm}–{ev.End:HH\\:mm}";
                var row = new StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 10 };
                row.Children.Add(new TextBlock { Text = time, Opacity = 0.7, FontSize = 13, Width = 110 });
                row.Children.Add(new TextBlock { Text = ev.Title, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
                CalendarPanel.Children.Add(row);
            }
        }
        CalendarSection.Visibility = Visibility.Visible;
    }

    private void BuildShortcuts(Ritmo.Core.Persistence.AppSettings settings)
    {
        ShortcutsPanel.Children.Clear();
        // Los enlaces viven en el entorno de trabajo (#74): muestra los del entorno por defecto.
        var env = settings.FocusEnvironments.FirstOrDefault(e => e.Id == settings.DefaultFocusEnvironmentId);
        var links = env?.Links ?? [];
        ShortcutsSection.Visibility = links.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var l in links)
        {
            var btn = new HyperlinkButton { Content = l.Title };
            ToolTipService.SetToolTip(btn, l.Url);
            var url = l.Url;
            btn.Click += (_, _) => OpenUrl(url);
            ShortcutsPanel.Children.Add(btn);
        }
    }

    private void BuildNotes(Ritmo.Core.Persistence.AppSettings settings)
    {
        NotesPanel.Children.Clear();

        // Post-its de las sesiones de HOY (#73) + notas generales (sueltas).
        var today = DateOnly.FromDateTime(DateTime.Now);
        var phase = settings.Plan.GetActivePhase(today) ?? settings.Plan.OrderedPhases.FirstOrDefault();
        var schedule = phase?.Schedule ?? settings.Schedule;
        var todayTitles = schedule.Sessions.Where(s => s.Day == today.DayOfWeek).Select(s => s.Title.Trim())
            .Concat(settings.OneOffSessions.Where(o => o.Date == today).Select(o => o.Title.Trim()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var notes = settings.Notes
            .Where(n => string.IsNullOrEmpty(n.SessionTitle) || todayTitles.Contains(n.SessionTitle!.Trim()))
            .OrderBy(n => n.SessionTitle is null ? 1 : 0)   // primero las de las sesiones de hoy
            .ThenBy(n => n.Order)
            .ToList();

        NotesSection.Visibility = notes.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var note in notes) NotesPanel.Children.Add(NoteCard(note));
    }

    private FrameworkElement NoteCard(StudyNote note)
    {
        var stack = new StackPanel { Spacing = 2 };
        if (!string.IsNullOrEmpty(note.SessionTitle))
            stack.Children.Add(new TextBlock
            {
                Text = note.SessionTitle, FontSize = 11, Opacity = 0.55,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
        stack.Children.Add(new TextBlock { Text = note.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        if (!string.IsNullOrWhiteSpace(note.Content))
        {
            var md = MarkdownRenderer.Build(note.Content);   // #72: render Markdown
            md.Opacity = 0.85;
            stack.Children.Add(md);
        }

        var border = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = stack
        };
        if (!string.IsNullOrWhiteSpace(note.AccentColor) && TryParseHex(note.AccentColor!, out var color))
        {
            border.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
            border.BorderThickness = new Thickness(4, 1, 1, 1);
        }
        return border;
    }

    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch { /* URL inválida: ignorar */ }
    }

    private static bool TryParseHex(string hex, out Windows.UI.Color color)
    {
        color = Microsoft.UI.Colors.Transparent;
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return false;
        try
        {
            byte r = System.Convert.ToByte(hex[..2], 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            color = Windows.UI.Color.FromArgb(255, r, g, b);
            return true;
        }
        catch { return false; }
    }

    private void StartBtn_Click(object sender, RoutedEventArgs e)
        => Navigator.GoToTimer(this, autoStart: true);

    private static string Capitalize(string s)
        => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}
