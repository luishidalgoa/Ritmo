using System;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Ritmo.Core.Model;
using Ritmo_App.Dialogs;
using Ritmo_App.Services;

namespace Ritmo_App;

/// <summary>
/// Página «Trabajo» (#84 V3): seguimiento laboral por PROYECTO con gráficos. Lista los proyectos
/// activos, un resumen global, y por cada uno: tarifa/objetivo, resumen del mes con proyección,
/// barras de horas por día, línea de acumulado vs objetivo, y anotación rápida de horas.
/// </summary>
public sealed partial class WorkPage : Page
{
    public WorkPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Build();
    }

    private static Brush Hex(string hex, double opacity = 1)
    {
        try
        {
            var h = hex.TrimStart('#');
            var c = Windows.UI.Color.FromArgb(255,
                Convert.ToByte(h.Substring(0, 2), 16),
                Convert.ToByte(h.Substring(2, 2), 16),
                Convert.ToByte(h.Substring(4, 2), 16));
            return new SolidColorBrush(c) { Opacity = opacity };
        }
        catch { return (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]; }
    }

    private void Build()
    {
        var s = AppState.Load();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var projects = s.WorkProjects.Where(p => !p.Archived).OrderBy(p => p.Order).ToList();

        EmptyText.Visibility = projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        GlobalCard.Visibility = projects.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        BuildGlobal(s, projects, today);

        ProjectsHost.Children.Clear();
        foreach (var p in projects)
            ProjectsHost.Children.Add(ProjectCard(s, p, today));
    }

    // ---------- Resumen global ----------

    private void BuildGlobal(Ritmo.Core.Persistence.AppSettings s, System.Collections.Generic.List<WorkProject> projects, DateOnly today)
    {
        GlobalHost.Children.Clear();
        if (projects.Count == 0) return;

        GlobalHost.Children.Add(new TextBlock { Text = "Este mes (todos los proyectos)", FontSize = 13, Opacity = 0.6 });

        double totalHours = 0;
        // Sumamos ganancias por proyecto (cada uno con su tarifa/moneda). Mostramos por moneda.
        var byCurrency = new System.Collections.Generic.Dictionary<string, decimal>();
        foreach (var p in projects)
        {
            var sum = WorkTracking.Summarize(s.WorkLog, p.Id, p.Rate, today);
            totalHours += sum.HoursThisMonth;
            var sym = p.CurrencySymbol;
            byCurrency[sym] = byCurrency.GetValueOrDefault(sym) + sum.EarningsThisMonth;
        }

        var money = string.Join("  ·  ", byCurrency.Where(kv => kv.Value > 0).Select(kv => $"{kv.Value:0.##} {kv.Key}"));
        var line = new TextBlock { FontSize = 22, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        line.Text = money.Length > 0 ? $"{totalHours:0.##} h  ·  {money}" : $"{totalHours:0.##} h";
        GlobalHost.Children.Add(line);
    }

    // ---------- Tarjeta de un proyecto ----------

    private FrameworkElement ProjectCard(Ritmo.Core.Persistence.AppSettings s, WorkProject p, DateOnly today)
    {
        var sum = WorkTracking.Summarize(s.WorkLog, p.Id, p.Rate, today);
        var sym = p.CurrencySymbol;

        var card = new StackPanel { Spacing = 12 };

        // --- Cabecera: punto de color + nombre + acciones ---
        var header = new Grid();
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(new Ellipse { Width = 14, Height = 14, Fill = Hex(p.ColorHex), VerticalAlignment = VerticalAlignment.Center });
        titleRow.Children.Add(new TextBlock { Text = p.Name, FontSize = 20, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        if (p.Rate > 0) titleRow.Children.Add(new TextBlock { Text = $"{p.Rate:0.##} {sym}/h", Opacity = 0.6, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        header.Children.Add(titleRow);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
        var edit = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 } };
        ToolTipService.SetToolTip(edit, "Editar proyecto");
        edit.Click += (_, _) => _ = EditProject(p);
        var del = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 } };
        ToolTipService.SetToolTip(del, "Eliminar proyecto");
        del.Click += (_, _) => _ = ConfirmDelete(p);
        actions.Children.Add(edit); actions.Children.Add(del);
        header.Children.Add(actions);
        card.Children.Add(header);

        // --- Resumen del mes + objetivo + proyección ---
        string monthLine = $"Este mes: {sum.HoursThisMonth:0.##} h";
        if (p.Rate > 0) monthLine += $"  ·  {sum.EarningsThisMonth:0.##} {sym}";
        if (p.MonthlyGoalHours > 0)
        {
            double prog = WorkTracking.GoalProgress(sum.HoursThisMonth, p.MonthlyGoalHours);
            monthLine += $"   ({prog * 100:0}% de {p.MonthlyGoalHours:0.##} h)";
        }
        card.Children.Add(new TextBlock { Text = monthLine, FontWeight = FontWeights.SemiBold, FontSize = 15, TextWrapping = TextWrapping.Wrap });

        var subParts = new System.Collections.Generic.List<string>();
        if (sum.HasProjection) subParts.Add($"Proyección fin de mes ~{sum.ProjectedMonthHours:0.#} h" + (p.Rate > 0 ? $" · {sum.ProjectedMonthEarnings:0.##} {sym}" : ""));
        subParts.Add($"Total histórico {sum.HoursTotal:0.##} h" + (p.Rate > 0 ? $" · {sum.EarningsTotal:0.##} {sym}" : ""));
        card.Children.Add(new TextBlock { Text = string.Join("   ·   ", subParts), Opacity = 0.65, FontSize = 12, TextWrapping = TextWrapping.Wrap });

        // --- Gráfico: barras de horas por día + línea de acumulado vs objetivo ---
        card.Children.Add(BuildChart(s, p, today));

        // --- Anotar horas de hoy ---
        card.Children.Add(BuildLogRow(p, today));

        // --- Últimas anotaciones ---
        var recent = s.WorkLog.Where(w => w.ProjectId == p.Id).OrderByDescending(w => w.Date).Take(6).ToList();
        if (recent.Count > 0)
        {
            var list = new StackPanel { Spacing = 2, Margin = new Thickness(0, 4, 0, 0) };
            foreach (var w in recent) list.Children.Add(LogEntryRow(p, w));
            card.Children.Add(list);
        }

        return new Border
        {
            CornerRadius = new CornerRadius(10), Padding = new Thickness(20),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], BorderThickness = new Thickness(1),
            Child = card
        };
    }

    // ---------- Gráfico (barras por día + línea acumulada vs objetivo) ----------

    private FrameworkElement BuildChart(Ritmo.Core.Persistence.AppSettings s, WorkProject p, DateOnly today)
    {
        var daily = WorkTracking.DailyHours(s.WorkLog, p.Id, today.Year, today.Month);
        var cumulative = WorkTracking.CumulativeHours(s.WorkLog, p.Id, today.Year, today.Month);
        int days = daily.Length;

        const double H = 120;       // alto del área de gráfico
        const double barW = 14;     // ancho por día
        const double gap = 4;
        double width = days * (barW + gap);

        var canvas = new Canvas { Height = H, Width = width, Margin = new Thickness(0, 4, 0, 0) };

        if (daily.All(h => h == 0) && p.MonthlyGoalHours == 0)
        {
            return new TextBlock { Text = "Aún no hay horas anotadas este mes.", Opacity = 0.5, FontSize = 12, Margin = new Thickness(0, 6, 0, 6) };
        }

        // El día de HOY (1-based) dentro del mes mostrado; si miramos otro mes, todo el mes.
        bool isCurrentMonth = today.Year == DateTime.Now.Year && today.Month == DateTime.Now.Month;
        int todayIdx = isCurrentMonth ? today.Day : days;   // hasta dónde llega la línea de acumulado

        // Escalas SEPARADAS: las barras (horas/día) y la línea de acumulado tienen magnitudes muy
        // distintas (p. ej. 6 h/día vs 120 h acumuladas). Si compartieran escala, las barras se
        // verían planas o la línea saturada. Por eso cada una tiene su propio máximo.
        double maxBar = daily.Length > 0 ? daily.Max() : 0;
        if (maxBar <= 0) maxBar = 1;
        double cumTop = Math.Max(cumulative.Length > 0 ? cumulative.Max() : 0, p.MonthlyGoalHours);
        if (cumTop <= 0) cumTop = 1;

        const double barArea = 70;   // las barras ocupan la parte baja
        var accent = Hex(p.ColorHex);
        var faint = Hex(p.ColorHex, 0.45);

        // Barras: horas de cada día (escala propia, en la franja inferior).
        for (int d = 0; d < days; d++)
        {
            double x = d * (barW + gap);
            double barH = daily[d] > 0 ? Math.Max(3, daily[d] / maxBar * barArea) : 0;
            bool isToday = isCurrentMonth && (d + 1) == today.Day;
            if (barH > 0)
            {
                var bar = new Rectangle
                {
                    Width = barW, Height = barH, RadiusX = 2, RadiusY = 2,
                    Fill = isToday ? accent : faint
                };
                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, H - barH);
                ToolTipService.SetToolTip(bar, $"Día {d + 1}: {daily[d]:0.##} h");
                canvas.Children.Add(bar);
            }
        }

        // Línea de objetivo (recta de meta acumulada: de 0 el día 1 a objetivo el último día).
        if (p.MonthlyGoalHours > 0)
        {
            var goalLine = new Polyline
            {
                Stroke = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            goalLine.Points.Add(new Windows.Foundation.Point(barW / 2, H));
            goalLine.Points.Add(new Windows.Foundation.Point(width - barW / 2, H - (p.MonthlyGoalHours / cumTop * H)));
            canvas.Children.Add(goalLine);
        }

        // Línea de acumulado real, SOLO hasta hoy (no plana hasta fin de mes).
        var cumLine = new Polyline { Stroke = accent, StrokeThickness = 2 };
        for (int d = 0; d < days && d < todayIdx; d++)
        {
            double x = d * (barW + gap) + barW / 2;
            double y = H - (cumulative[d] / cumTop * H);
            cumLine.Points.Add(new Windows.Foundation.Point(x, y));
        }
        if (cumLine.Points.Count > 1) canvas.Children.Add(cumLine);

        var wrap = new StackPanel { Spacing = 4 };
        // Leyenda.
        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        legend.Children.Add(LegendDot(faint, "Horas por día"));
        legend.Children.Add(LegendDot(accent, "Acumulado del mes"));
        if (p.MonthlyGoalHours > 0) legend.Children.Add(LegendDot((Brush)Application.Current.Resources["TextFillColorTertiaryBrush"], "Objetivo"));
        wrap.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = canvas });
        wrap.Children.Add(legend);
        return wrap;
    }

    private static FrameworkElement LegendDot(Brush brush, string text)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new Rectangle { Width = 12, Height = 4, RadiusX = 2, RadiusY = 2, Fill = brush, VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = text, FontSize = 11, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center });
        return sp;
    }

    // ---------- Anotar horas + editar anotaciones ----------

    private FrameworkElement BuildLogRow(WorkProject p, DateOnly today)
    {
        var hoursBox = new NumberBox { PlaceholderText = "Horas de hoy", Minimum = 0, SmallChange = 0.5, Width = 160,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var noteBox = new TextBox { PlaceholderText = "Nota (opcional)", MinWidth = 160 };
        var btn = new Button { Content = "Anotar horas de hoy" };
        void Log()
        {
            if (double.IsNaN(hoursBox.Value) || hoursBox.Value <= 0) return;
            AppState.Config.AddWorkHours(p.Id, today, hoursBox.Value, noteBox.Text ?? "");
            Build();
        }
        btn.Click += (_, _) => Log();

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        row.Children.Add(hoursBox); row.Children.Add(noteBox); row.Children.Add(btn);
        return row;
    }

    private FrameworkElement LogEntryRow(WorkProject p, WorkLogEntry w)
    {
        var txt = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        txt.Text = $"{w.Date:dd/MM} · {w.Hours:0.##} h" + (string.IsNullOrWhiteSpace(w.Note) ? "" : $"  — {w.Note}");
        Grid.SetColumn(txt, 0);

        var del = new Button { Content = new FontIcon { Glyph = "", FontSize = 12 }, Padding = new Thickness(6), MinWidth = 0,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
        ToolTipService.SetToolTip(del, "Eliminar anotación");
        del.Click += (_, _) => { AppState.Config.RemoveWorkLogEntry(w.Id); Build(); };
        Grid.SetColumn(del, 1);

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(txt); g.Children.Add(del);
        return g;
    }

    // ---------- Alta / edición / borrado de proyecto ----------

    private async void AddProjectBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new WorkProjectDialog { XamlRoot = this.XamlRoot };
        dlg.LoadDefaults();
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        AppState.Config.AddWorkProject(dlg.ProjectName, dlg.Rate, dlg.GoalHours, dlg.ColorHex, dlg.CurrencyCode);
        Build();
    }

    private async System.Threading.Tasks.Task EditProject(WorkProject p)
    {
        var dlg = new WorkProjectDialog { XamlRoot = this.XamlRoot };
        dlg.LoadFrom(p);
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        AppState.Config.UpdateWorkProject(p.Id, dlg.ProjectName, dlg.Rate, dlg.GoalHours, dlg.ColorHex, dlg.CurrencyCode);
        Build();
    }

    private async System.Threading.Tasks.Task ConfirmDelete(WorkProject p)
    {
        var confirm = new ContentDialog
        {
            XamlRoot = this.XamlRoot, Title = "Eliminar proyecto",
            Content = $"¿Eliminar «{p.Name}» y todas sus horas anotadas? No se puede deshacer.",
            PrimaryButtonText = "Eliminar", CloseButtonText = "Cancelar", DefaultButton = ContentDialogButton.Close
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        AppState.Config.RemoveWorkProject(p.Id);
        Build();
    }
}
