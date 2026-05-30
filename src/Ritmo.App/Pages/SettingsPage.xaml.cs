using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ritmo.Core.Focus;
using Ritmo_App.Dialogs;
using Ritmo_App.Services;

namespace Ritmo_App;

/// <summary>
/// Pantalla de Ajustes: configura el Pomodoro, el rango horario de la rejilla,
/// los entornos de concentración (#53) y el tema. Persiste vía ConfigurationService
/// (compartido con la UI y el MCP).
/// </summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadValues();
    }

    private void LoadValues()
    {
        var s = AppState.Load();
        FocusBox.Value = s.Pomodoro.Focus.TotalMinutes;
        ShortBox.Value = s.Pomodoro.ShortBreak.TotalMinutes;
        LongBox.Value = s.Pomodoro.LongBreak.TotalMinutes;
        CyclesBox.Value = s.Pomodoro.FocusesPerLongBreak;

        DayStartPicker.Time = s.ViewConfig.DayStart.ToTimeSpan();
        DayEndPicker.Time = s.ViewConfig.DayEnd.ToTimeSpan();

        ThemeBox.SelectedIndex = (this.ActualTheme) switch
        {
            ElementTheme.Light => 1,
            ElementTheme.Dark => 2,
            _ => 0
        };

        BuildEnvList();
        BuildNotes();
        PomodoroHelp.Content = HelpHint.Icon("pomodoro");   // ayuda (#93)
        _ = LoadAutostartState();
    }

    // ---------- Notas (#55). Los enlaces viven en los entornos de trabajo (#74). ----------

    private void BuildNotes()
    {
        var s = AppState.Load();
        NotesList.Children.Clear();
        foreach (var note in s.Notes.OrderBy(n => n.Order))
            NotesList.Children.Add(NoteRow(note));
    }

    private FrameworkElement NoteRow(Ritmo.Core.Model.StudyNote note)
    {
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = note.Title, FontWeight = FontWeights.SemiBold });
        if (!string.IsNullOrWhiteSpace(note.Content))
            info.Children.Add(new TextBlock
            {
                Text = note.Content, Opacity = 0.65, FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1
            });
        Grid.SetColumn(info, 0);

        var edit = new Button { Content = new SymbolIcon(Symbol.Edit) };
        ToolTipService.SetToolTip(edit, "Editar");
        edit.Click += (_, _) => _ = EditNote(note);
        Grid.SetColumn(edit, 1);

        var del = new Button { Content = new SymbolIcon(Symbol.Delete), Margin = new Thickness(6, 0, 0, 0) };
        ToolTipService.SetToolTip(del, "Eliminar");
        del.Click += (_, _) => { AppState.Config.RemoveNote(note.Id); BuildNotes(); };
        Grid.SetColumn(del, 2);

        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(info); grid.Children.Add(edit); grid.Children.Add(del);

        return new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 8, 8), Child = grid
        };
    }

    private async void AddNoteBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new NoteDialog { XamlRoot = this.XamlRoot };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary && dlg.TitleText.Length > 0)
        {
            AppState.Config.AddNote(dlg.TitleText, dlg.ContentText);
            BuildNotes();
        }
    }

    private async Task EditNote(Ritmo.Core.Model.StudyNote note)
    {
        var dlg = new NoteDialog { XamlRoot = this.XamlRoot };
        dlg.LoadFrom(note);
        if (await dlg.ShowAsync() == ContentDialogResult.Primary && dlg.TitleText.Length > 0)
        {
            AppState.Config.UpdateNote(note.Id, dlg.TitleText, dlg.ContentText);
            BuildNotes();
        }
    }

    // ---------- Segundo plano y arranque (#24/#20) ----------

    private bool _autostartLoading;

    private async Task LoadAutostartState()
    {
        _autostartLoading = true;
        try
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync("RitmoStartup");
            switch (task.State)
            {
                case Windows.ApplicationModel.StartupTaskState.Enabled:
                    AutostartSwitch.IsOn = true; AutostartSwitch.IsEnabled = true; AutostartHint.Text = "";
                    break;
                case Windows.ApplicationModel.StartupTaskState.Disabled:
                    AutostartSwitch.IsOn = false; AutostartSwitch.IsEnabled = true; AutostartHint.Text = "";
                    break;
                case Windows.ApplicationModel.StartupTaskState.DisabledByUser:
                    AutostartSwitch.IsOn = false; AutostartSwitch.IsEnabled = false;
                    AutostartHint.Text = "Desactivado desde el Administrador de tareas de Windows. Actívalo allí (pestaña Inicio).";
                    break;
                default:
                    AutostartSwitch.IsOn = false; AutostartSwitch.IsEnabled = false;
                    AutostartHint.Text = "El arranque automático lo gestiona una directiva del sistema.";
                    break;
            }
        }
        catch (Exception ex)
        {
            AutostartSwitch.IsEnabled = false;
            AutostartHint.Text = $"No disponible: {ex.Message}";
        }
        finally { _autostartLoading = false; }
    }

    private async void AutostartSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_autostartLoading) return;
        try
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync("RitmoStartup");
            if (AutostartSwitch.IsOn)
            {
                var state = await task.RequestEnableAsync();
                if (state != Windows.ApplicationModel.StartupTaskState.Enabled)
                {
                    AutostartSwitch.IsOn = false;
                    AutostartHint.Text = "Windows no permitió activarlo (revisa el Administrador de tareas).";
                }
            }
            else
            {
                task.Disable();
            }
        }
        catch (Exception ex)
        {
            AutostartHint.Text = $"No se pudo cambiar: {ex.Message}";
        }
    }

    private void ExitBtn_Click(object sender, RoutedEventArgs e) => MainWindow.Current?.ExitApp();

    // ---------- Copia de seguridad: exportar / importar (#56) ----------

    private static nint WindowHandle()
        => MainWindow.Current is null ? 0 : WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current);

    private async void ExportBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "ritmo-config"
            };
            picker.FileTypeChoices.Add("Configuración de Ritmo", new System.Collections.Generic.List<string> { ".json" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle());

            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            await Windows.Storage.FileIO.WriteTextAsync(file, AppState.Config.ExportJson());
            BackupStatus.Text = $"✓ Exportado a {file.Name}";
        }
        catch (Exception ex) { BackupStatus.Text = $"⚠ {ex.Message}"; }
    }

    private async void ImportBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".json");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle());

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            // Importar reemplaza TODO: confirmar antes.
            var confirm = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Importar configuración",
                Content = "Esto reemplazará toda tu configuración actual por la del archivo. ¿Continuar?",
                PrimaryButtonText = "Importar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            var json = await Windows.Storage.FileIO.ReadTextAsync(file);
            var r = AppState.Config.ImportJson(json);
            if (r.Success)
            {
                ScheduleHost.Instance.Start();   // re-leer el horario importado
                LoadValues();                    // refrescar la pantalla
                BackupStatus.Text = "✓ Configuración importada";
            }
            else
            {
                BackupStatus.Text = $"⚠ {r.Message}";
            }
        }
        catch (Exception ex) { BackupStatus.Text = $"⚠ {ex.Message}"; }
    }

    // ---------- Entornos de concentración (#53) ----------

    private void BuildEnvList()
    {
        var s = AppState.Load();
        EnvList.Children.Clear();
        EnvEmpty.Visibility = s.FocusEnvironments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var env in s.FocusEnvironments)
            EnvList.Children.Add(EnvRow(env, env.Id == s.DefaultFocusEnvironmentId));
    }

    private FrameworkElement EnvRow(FocusEnvironment env, bool isDefault)
    {
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = env.Name, FontWeight = FontWeights.SemiBold });
        info.Children.Add(new TextBlock
        {
            Text = Summary(env), Opacity = 0.65, FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(info, 0);

        // Estrella: en acento si es el predeterminado; atenuada si no.
        var starIcon = new SymbolIcon(Symbol.Favorite);
        if (isDefault)
            starIcon.Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
        else
            starIcon.Opacity = 0.45;
        var star = new Button
        {
            Content = starIcon,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0)
        };
        ToolTipService.SetToolTip(star, isDefault ? "Predeterminado" : "Marcar como predeterminado");
        star.Click += (_, _) => { AppState.Config.SetDefaultEnvironment(env.Id); BuildEnvList(); };
        Grid.SetColumn(star, 1);

        var edit = new Button { Content = new SymbolIcon(Symbol.Edit) };
        ToolTipService.SetToolTip(edit, "Editar");
        edit.Click += (_, _) => _ = EditEnv(env);
        Grid.SetColumn(edit, 2);

        var del = new Button { Content = new SymbolIcon(Symbol.Delete), Margin = new Thickness(6, 0, 0, 0) };
        ToolTipService.SetToolTip(del, "Eliminar");
        del.Click += (_, _) => _ = DeleteEnv(env);
        Grid.SetColumn(del, 3);

        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(info); grid.Children.Add(star); grid.Children.Add(edit); grid.Children.Add(del);

        return new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 8, 8),
            Child = grid
        };
    }

    private static string Summary(FocusEnvironment env)
    {
        var parts = new System.Collections.Generic.List<string>
        {
            env.PomodoroPreset switch { "Classic" => "Clásico", "DeepWork" => "Profundo", _ => "Pomodoro por defecto" }
        };
        if (env.EnableDoNotDisturb) parts.Add("No molestar");
        if (env.OpenStudyListInEdge) parts.Add("Edge");
        if (env.Music is not null) parts.Add($"música: {env.Music.Name}");
        if (env.AppsToClose.Count > 0) parts.Add($"cierra {env.AppsToClose.Count} app(s)");
        if (env.BlockedWebsites.Count > 0) parts.Add($"bloquea {env.BlockedWebsites.Count} web(s)");
        return string.Join("  ·  ", parts);
    }

    private async void AddEnvBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new EnvironmentDialog { XamlRoot = this.XamlRoot };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            AppState.Config.UpsertEnvironment(dlg.ToEnvironment());
            BuildEnvList();
        }
    }

    private async Task EditEnv(FocusEnvironment env)
    {
        var dlg = new EnvironmentDialog { XamlRoot = this.XamlRoot };
        dlg.LoadFrom(env);
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            AppState.Config.UpsertEnvironment(dlg.ToEnvironment());
            BuildEnvList();
        }
    }

    private async Task DeleteEnv(FocusEnvironment env)
    {
        var confirm = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Eliminar entorno",
            Content = $"¿Eliminar «{env.Name}»? Esta acción no se puede deshacer.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            AppState.Config.RemoveEnvironment(env.Id);
            BuildEnvList();
        }
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        int Val(NumberBox b, int fallback) => double.IsNaN(b.Value) ? fallback : (int)b.Value;

        var r1 = AppState.Config.SetPomodoro(
            Val(FocusBox, 50), Val(ShortBox, 10), Val(LongBox, 20), Val(CyclesBox, 2));

        var start = TimeOnly.FromTimeSpan(DayStartPicker.Time);
        var end = TimeOnly.FromTimeSpan(DayEndPicker.Time);
        var r2 = AppState.Config.SetViewHours(start, end);

        if (ThemeBox.SelectedItem is ComboBoxItem it && it.Tag is string tag)
        {
            var theme = tag switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
            if (this.XamlRoot?.Content is FrameworkElement root)
                root.RequestedTheme = theme;
        }

        SaveStatus.Text = (r1.Success && r2.Success)
            ? "✓ Guardado"
            : $"⚠ {(!r1.Success ? r1.Message : r2.Message)}";
    }
}
