using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ritmo.Core.Focus;
using Ritmo.Core.Model;
using Ritmo.Core.Pomodoro;
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
        GranularityBox.SelectedIndex = s.ViewConfig.GranularityMinutes switch { 30 => 1, 15 => 2, _ => 0 };
        DayPreviewToggle.IsOn = s.ViewConfig.ShowDayPreviewOnFocusStart;
        SelectComboByTag(DefaultPreAlertBox, s.ViewConfig.DefaultPreAlertMinutes.ToString());

        BuildCategories(s);
        RefreshConnections(s);
        VersionText.Text = $"Versión actual: {AppVersionInfo.Current}";

        ThemeBox.SelectedIndex = (this.ActualTheme) switch
        {
            ElementTheme.Light => 1,
            ElementTheme.Dark => 2,
            _ => 0
        };

        BuildEnvList();
        BuildPhases();
        BuildRhythms();
        BuildNotes();
        LoadNavidromeState(s);
        BuildCalendarFeeds();
        PomodoroHelp.Content = HelpHint.Icon("pomodoro");   // ayuda (#93)
        RhythmsHelp.Content = HelpHint.Icon("rhythm");      // ayuda (#96)
        _ = LoadAutostartState();
    }

    // ---------- Música: conexión global a Navidrome (#107) ----------

    private void LoadNavidromeState(Ritmo.Core.Persistence.AppSettings s)
    {
        NavServerBox.Text = s.NavidromeServerUrl ?? "";
        NavUserBox.Text = s.NavidromeUser ?? "";
        bool connected = NavidromeService.IsConnected(s);
        NavStatus.Text = connected ? "✓ Conectado" : "No conectado";
        NavHeaderStatus.Text = connected ? "· Conectado" : "";
    }

    private async void NavConnectBtn_Click(object sender, RoutedEventArgs e)
    {
        var server = NavServerBox.Text.Trim();
        var user = NavUserBox.Text.Trim();
        var pass = NavPassBox.Password;
        if (server.Length == 0 || user.Length == 0 || pass.Length == 0)
        {
            NavStatus.Text = "Rellena servidor, usuario y contraseña.";
            return;
        }
        NavConnectBtn.IsEnabled = false;
        NavStatus.Text = "Conectando…";
        try
        {
            var playlists = await NavidromeService.GetPlaylistsAsync(server, user, pass);
            AppState.Config.SetNavidromeConnection(server, user);
            NavidromeService.StorePassword(pass);
            NavPassBox.Password = "";
            NavStatus.Text = $"✓ Conectado · {playlists.Count} playlist(s)";
            NavHeaderStatus.Text = "· Conectado";
        }
        catch (Exception ex)
        {
            NavStatus.Text = "⚠ " + ex.Message;
        }
        finally { NavConnectBtn.IsEnabled = true; }
    }

    private void NavDisconnectBtn_Click(object sender, RoutedEventArgs e)
    {
        AppState.Config.ClearNavidromeConnection();
        NavidromeService.ClearPassword();
        NavServerBox.Text = ""; NavUserBox.Text = ""; NavPassBox.Password = "";
        NavStatus.Text = "No conectado";
        NavHeaderStatus.Text = "";
    }

    // ---------- Calendarios (suscripción ICS, #112) ----------

    private void BuildCalendarFeeds()
    {
        var s = AppState.Load();
        CalFeedsList.Children.Clear();
        foreach (var f in s.CalendarFeeds)
        {
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = f.Name, FontWeight = FontWeights.SemiBold });
            info.Children.Add(new TextBlock { Text = f.Url, Opacity = 0.6, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(info, 0);

            var del = new Button { Content = new SymbolIcon(Symbol.Delete), Margin = new Thickness(6, 0, 0, 0) };
            ToolTipService.SetToolTip(del, "Quitar calendario");
            var id = f.Id;
            del.Click += (_, _) => { AppState.Config.RemoveCalendarFeed(id); BuildCalendarFeeds(); };
            Grid.SetColumn(del, 1);

            var grid = new Grid { ColumnSpacing = 6 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(info); grid.Children.Add(del);
            CalFeedsList.Children.Add(new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 8, 6), Child = grid
            });
        }
    }

    private void CalAddBtn_Click(object sender, RoutedEventArgs e)
    {
        var r = AppState.Config.AddCalendarFeed(CalNameBox.Text, CalUrlBox.Text);
        if (r.Success) { CalNameBox.Text = ""; CalUrlBox.Text = ""; BuildCalendarFeeds(); }
    }

    // ---------- Ritmos Pomodoro personalizados (#96) ----------

    private void BuildRhythms()
    {
        var s = AppState.Load();
        RhythmsList.Children.Clear();
        RhythmsEmpty.Visibility = s.Rhythms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var r in s.Rhythms) RhythmsList.Children.Add(RhythmRow(r));
    }

    private FrameworkElement RhythmRow(PomodoroRhythm r)
    {
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = r.Name, FontWeight = FontWeights.SemiBold });
        info.Children.Add(new TextBlock
        {
            Text = $"Concentración {r.FocusMinutes} · corto {r.ShortBreakMinutes} · largo {r.LongBreakMinutes} cada {r.FocusesPerLongBreak} focos",
            Opacity = 0.65, FontSize = 12
        });
        Grid.SetColumn(info, 0);

        var edit = new Button { Content = new SymbolIcon(Symbol.Edit) };
        ToolTipService.SetToolTip(edit, "Editar");
        edit.Click += (_, _) => _ = EditRhythm(r);
        Grid.SetColumn(edit, 1);

        var del = new Button { Content = new SymbolIcon(Symbol.Delete), Margin = new Thickness(6, 0, 0, 0) };
        ToolTipService.SetToolTip(del, "Eliminar");
        del.Click += (_, _) => { AppState.Config.RemoveRhythm(r.Id); BuildRhythms(); };
        Grid.SetColumn(del, 2);

        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(info); grid.Children.Add(edit); grid.Children.Add(del);

        return new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 8, 8),
            Child = grid
        };
    }

    private async void AddRhythmBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new RhythmDialog { XamlRoot = this.XamlRoot };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            AppState.Config.AddRhythm(dlg.RhythmName, dlg.FocusMin, dlg.ShortMin, dlg.LongMin, dlg.FocusesPerLong);
            BuildRhythms();
        }
    }

    private async Task EditRhythm(PomodoroRhythm r)
    {
        var dlg = new RhythmDialog { XamlRoot = this.XamlRoot };
        dlg.LoadFrom(r);
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            AppState.Config.UpdateRhythm(r.Id, dlg.RhythmName, dlg.FocusMin, dlg.ShortMin, dlg.LongMin, dlg.FocusesPerLong);
            BuildRhythms();
        }
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

    // ---------- Fases del plan (#46) ----------

    private void BuildPhases()
    {
        var s = AppState.Load();
        PhasesList.Children.Clear();
        var active = s.Plan.GetActivePhase(DateOnly.FromDateTime(DateTime.Now));
        foreach (var ph in s.Plan.OrderedPhases)
            PhasesList.Children.Add(PhaseRow(ph, active is not null && ph.Name == active.Name));
    }

    private FrameworkElement PhaseRow(Ritmo.Core.Model.SchedulePhase ph, bool isActive)
    {
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        titleRow.Children.Add(new TextBlock { Text = ph.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        if (isActive)
        {
            titleRow.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 1, 8, 1), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = "Vigente", FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"] }
            });
        }
        info.Children.Add(titleRow);
        var to = ph.ValidTo is { } end ? end.ToString("dd/MM/yyyy") : "indefinida";
        info.Children.Add(new TextBlock
        {
            Text = $"{ph.ValidFrom:dd/MM/yyyy} → {to}   ·   {ph.Schedule.Sessions.Count} sesión(es)",
            Opacity = 0.65, FontSize = 12
        });
        Grid.SetColumn(info, 0);

        var edit = new Button { Content = new SymbolIcon(Symbol.Edit) };
        ToolTipService.SetToolTip(edit, "Editar fase");
        edit.Click += (_, _) => _ = EditPhase(ph);
        Grid.SetColumn(edit, 1);

        var dup = new Button { Content = new SymbolIcon(Symbol.Copy), Margin = new Thickness(6, 0, 0, 0) };
        ToolTipService.SetToolTip(dup, "Duplicar fase (copia su horario)");
        dup.Click += (_, _) => _ = DuplicatePhaseUi(ph);
        Grid.SetColumn(dup, 2);

        var del = new Button { Content = new SymbolIcon(Symbol.Delete), Margin = new Thickness(6, 0, 0, 0) };
        ToolTipService.SetToolTip(del, "Eliminar fase");
        del.Click += (_, _) => _ = DeletePhase(ph);
        Grid.SetColumn(del, 3);

        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(info); grid.Children.Add(edit); grid.Children.Add(dup); grid.Children.Add(del);

        return new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 8, 8), Child = grid
        };
    }

    private async void AddPhaseBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PhaseDialog { XamlRoot = this.XamlRoot };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            var r = AppState.Config.AddPhase(dlg.PhaseName, dlg.ValidFrom, dlg.ValidTo);
            if (!r.Success) await InfoDialog(r.Message);
            BuildPhases();
        }
    }

    private async Task EditPhase(Ritmo.Core.Model.SchedulePhase ph)
    {
        var dlg = new PhaseDialog { XamlRoot = this.XamlRoot };
        dlg.LoadFrom(ph);
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            var r = AppState.Config.UpdatePhase(ph.Name, dlg.PhaseName, dlg.ValidFrom, dlg.ValidTo);
            if (!r.Success) await InfoDialog(r.Message);
            BuildPhases();
        }
    }

    private async Task DuplicatePhaseUi(Ritmo.Core.Model.SchedulePhase ph)
    {
        var dlg = new PhaseDialog { XamlRoot = this.XamlRoot };
        dlg.LoadForDuplicate(ph);
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            var r = AppState.Config.DuplicatePhase(ph.Name, dlg.PhaseName, dlg.ValidFrom, dlg.ValidTo);
            if (!r.Success) await InfoDialog(r.Message);
            BuildPhases();
        }
    }

    private async Task DeletePhase(Ritmo.Core.Model.SchedulePhase ph)
    {
        var confirm = new ContentDialog
        {
            XamlRoot = this.XamlRoot, Title = "Eliminar fase",
            Content = $"¿Eliminar la fase «{ph.Name}» y su horario? Esta acción no se puede deshacer.",
            PrimaryButtonText = "Eliminar", CloseButtonText = "Cancelar", DefaultButton = ContentDialogButton.Close
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            var r = AppState.Config.RemovePhase(ph.Name);
            if (!r.Success) await InfoDialog(r.Message);
            BuildPhases();
        }
    }

    private async Task InfoDialog(string msg)
        => await new ContentDialog { XamlRoot = this.XamlRoot, Title = "Fases", Content = msg, CloseButtonText = "Vale" }.ShowAsync();

    // ---------- Entornos de concentración (#53) ----------

    private void BuildEnvList()
    {
        var s = AppState.Load();
        EnvList.Children.Clear();
        EnvEmpty.Visibility = s.FocusEnvironments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var env in s.FocusEnvironments)
            EnvList.Children.Add(EnvExpander(env, env.Id == s.DefaultFocusEnvironmentId));

        BuildKindEnvMap();   // la asignación tipo→entorno depende de la lista de entornos (#70)
    }

    // ---------- Entorno por tipo de bloque (#70) ----------

    private bool _loadingKindMap;

    private void BuildKindEnvMap()
    {
        if (KindEnvList is null) return;
        _loadingKindMap = true;
        KindEnvList.Children.Clear();

        var s = AppState.Load();
        if (s.FocusEnvironments.Count == 0)
        {
            KindEnvHint.Visibility = Visibility.Visible;
            _loadingKindMap = false;
            return;
        }
        KindEnvHint.Visibility = Visibility.Collapsed;

        foreach (var category in s.Categories.Where(c => c.IsFocus).OrderBy(c => c.Order))
        {
            var label = new TextBlock { Text = category.Name, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 0);

            var combo = new ComboBox { MinWidth = 240, HorizontalAlignment = HorizontalAlignment.Right };
            combo.Items.Add(new ComboBoxItem { Content = "Por defecto (★)", Tag = "" });
            foreach (var env in s.FocusEnvironments)
                combo.Items.Add(new ComboBoxItem { Content = env.Name, Tag = env.Id });

            var current = s.EnvironmentByKind.TryGetValue(category.Id, out var id) ? id : "";
            combo.SelectedIndex = 0;
            for (int i = 0; i < combo.Items.Count; i++)
                if (combo.Items[i] is ComboBoxItem it && (string)it.Tag == current) { combo.SelectedIndex = i; break; }

            var thisCategoryId = category.Id;
            combo.SelectionChanged += (_, _) =>
            {
                if (_loadingKindMap) return;
                var sel = (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
                if (string.IsNullOrEmpty(sel)) AppState.Config.ClearEnvironmentKind(thisCategoryId);
                else AppState.Config.MapEnvironmentToKind(thisCategoryId, sel);
            };
            Grid.SetColumn(combo, 1);

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(label); grid.Children.Add(combo);
            KindEnvList.Children.Add(grid);
        }
        _loadingKindMap = false;
    }

    // Cada entorno es un colapsable (#76) cuyo cuerpo lista sus módulos; al pulsar un
    // módulo se abre su vista de detalle (el editor en modo módulo). Reúsa #53 y #74.
    private FrameworkElement EnvExpander(FocusEnvironment env, bool isDefault)
    {
        // ---- Cabecera: nombre + resumen + ★ predeterminado + eliminar ----
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = env.Name, FontWeight = FontWeights.SemiBold });
        info.Children.Add(new TextBlock
        {
            Text = Summary(env), Opacity = 0.65, FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(info, 0);

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
        var starTip = isDefault ? "Predeterminado" : "Marcar como predeterminado";
        ToolTipService.SetToolTip(star, starTip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(star, $"{env.Name}: {starTip}");
        star.Click += (_, _) => { AppState.Config.SetDefaultEnvironment(env.Id); BuildEnvList(); };
        Grid.SetColumn(star, 1);

        var del = new Button
        {
            Content = new SymbolIcon(Symbol.Delete),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(2, 0, 0, 0)
        };
        ToolTipService.SetToolTip(del, "Eliminar entorno");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(del, $"Eliminar entorno {env.Name}");
        del.Click += (_, _) => _ = DeleteEnv(env);
        Grid.SetColumn(del, 2);

        var header = new Grid { ColumnSpacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(info); header.Children.Add(star); header.Children.Add(del);

        // ---- Cuerpo: una fila por módulo ----
        var body = new StackPanel { Spacing = 6 };
        foreach (var mod in EnvironmentModules.For(env))
            body.Children.Add(ModuleRow(env, mod));

        return new Expander
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Header = header,
            Content = body
        };
    }

    // Glifo (Segoe Fluent) por módulo.
    private static string ModuleGlyph(EnvironmentModuleKind kind) => kind switch
    {
        EnvironmentModuleKind.Focus => "",   // cronómetro
        EnvironmentModuleKind.Links => "",   // enlace
        EnvironmentModuleKind.Tasks => "",   // lista
        EnvironmentModuleKind.Tools => "",   // herramienta
        _ => ""
    };

    // Fila clicable de un módulo: glifo + título + resumen + chevron (o «Próximamente»).
    private FrameworkElement ModuleRow(FocusEnvironment env, EnvironmentModuleInfo mod)
    {
        var icon = new FontIcon { Glyph = ModuleGlyph(mod.Kind), FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(icon, 0);

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = mod.Title, FontWeight = FontWeights.SemiBold, FontSize = 13 });
        texts.Children.Add(new TextBlock { Text = mod.Summary, Opacity = 0.65, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(texts, 1);

        // Pista a la derecha: chevron si es accionable, badge «Próximamente» si no.
        FrameworkElement hint = mod.Available
            ? new FontIcon { Glyph = "", FontSize = 12, Opacity = 0.55, VerticalAlignment = VerticalAlignment.Center }
            : new TextBlock { Text = "Próximamente", FontSize = 11, Opacity = 0.55, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(hint, 2);

        var grid = new Grid { ColumnSpacing = 12, Padding = new Thickness(4, 2, 4, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(icon); grid.Children.Add(texts); grid.Children.Add(hint);

        var btn = new Button
        {
            Content = grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsEnabled = mod.Available
        };
        // Nombre accesible (lectores de pantalla / automatización): el contenido es un
        // Grid, así que sin esto el botón quedaría sin Name.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(btn, $"{mod.Title}. {mod.Summary}");
        if (mod.Available)
            btn.Click += (_, _) => _ = EditEnvModule(env, mod.Kind);
        return btn;
    }

    /// <summary>Abre la vista de detalle de un módulo del entorno (#76).</summary>
    private async Task EditEnvModule(FocusEnvironment env, EnvironmentModuleKind kind)
    {
        // Herramientas externas (#78): de momento «abrir el workspace en el navegador».
        if (kind == EnvironmentModuleKind.Tools) { await OpenToolsModule(env); return; }
        // Tareas (#125): lista de to-dos propia del entorno.
        if (kind == EnvironmentModuleKind.Tasks) { await OpenTasksModule(env); return; }

        // Concentración (#53) / Enlaces (#74): el editor restringido a ese módulo.
        var dlg = new EnvironmentDialog { XamlRoot = this.XamlRoot };
        dlg.LoadFrom(env);
        dlg.ScopeToModule(kind);
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            AppState.Config.UpsertEnvironment(dlg.ToEnvironment());
            BuildEnvList();
        }
    }

    /// <summary>Detalle del módulo Herramientas externas: abrir el workspace en el navegador (#78).</summary>
    private async Task OpenToolsModule(FocusEnvironment env)
    {
        var urls = EnvironmentWorkspace.Urls(env);
        var hasLinks = urls.Count > 0;

        var body = new StackPanel { Spacing = 10, Width = 360 };
        body.Children.Add(new TextBlock
        {
            Text = "Abre de golpe todos los enlaces de este entorno en una ventana nueva de tu navegador por defecto.",
            TextWrapping = TextWrapping.Wrap, Opacity = 0.8, FontSize = 13
        });

        if (hasLinks)
        {
            var list = new StackPanel { Spacing = 2 };
            foreach (var u in urls)
                list.Children.Add(new TextBlock { Text = "•  " + u, FontSize = 12, Opacity = 0.7, TextTrimming = TextTrimming.CharacterEllipsis });
            body.Children.Add(list);
        }
        else
        {
            body.Children.Add(new TextBlock
            {
                Text = "Este entorno aún no tiene enlaces. Añádelos desde el módulo «Enlaces».",
                TextWrapping = TextWrapping.Wrap, Opacity = 0.6, FontSize = 12
            });
        }

        body.Children.Add(new TextBlock
        {
            Text = "Próximamente: vincular calendario externo y más herramientas.",
            TextWrapping = TextWrapping.Wrap, Opacity = 0.5, FontSize = 12
        });

        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = $"Herramientas externas · {env.Name}",
            Content = body,
            PrimaryButtonText = "Abrir workspace",
            CloseButtonText = "Cerrar",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = hasLinks
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            DefaultBrowser.OpenLinksInNewWindow(urls);
    }

    /// <summary>Detalle del módulo Tareas: listar/añadir/marcar/reordenar/borrar to-dos del entorno (#125).</summary>
    private async Task OpenTasksModule(FocusEnvironment env)
    {
        var listPanel = new StackPanel { Spacing = 4 };

        // Alta de tarea.
        var input = new TextBox { PlaceholderText = "Nueva tarea…", MinWidth = 250 };
        var addBtn = new Button { Content = "Añadir" };
        var addRow = new Grid { ColumnSpacing = 6 };
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(input, 0); Grid.SetColumn(addBtn, 1);
        addRow.Children.Add(input); addRow.Children.Add(addBtn);

        // Re-pinta la lista desde el estado actual del entorno (las mutaciones van por la fachada).
        void Render()
        {
            listPanel.Children.Clear();
            var cur = AppState.Load().FocusEnvironments.FirstOrDefault(e => e.Id == env.Id);
            var tasks = cur?.Tasks.OrderBy(t => t.Order).ToList() ?? new System.Collections.Generic.List<EnvironmentTask>();
            if (tasks.Count == 0)
            {
                listPanel.Children.Add(new TextBlock { Text = "Aún no hay tareas. Añade la primera arriba.", Opacity = 0.6, FontSize = 12 });
                return;
            }
            for (int i = 0; i < tasks.Count; i++)
            {
                var t = tasks[i];
                bool isFirst = i == 0, isLast = i == tasks.Count - 1;

                var check = new CheckBox
                {
                    IsChecked = t.Done,
                    Content = new TextBlock { Text = t.Text, TextWrapping = TextWrapping.Wrap, Opacity = t.Done ? 0.5 : 1.0 },
                    MinWidth = 0
                };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(check, t.Text);
                check.Click += (_, _) => { AppState.Config.ToggleEnvironmentTask(env.Id, t.Id); Render(); };
                Grid.SetColumn(check, 0);

                var up = new Button { Content = new FontIcon { Glyph = "", FontSize = 12 }, IsEnabled = !isFirst, Padding = new Thickness(6, 2, 6, 2) };
                ToolTipService.SetToolTip(up, "Subir");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(up, $"Subir tarea {t.Text}");
                up.Click += (_, _) => { AppState.Config.MoveEnvironmentTask(env.Id, t.Id, true); Render(); };
                Grid.SetColumn(up, 1);

                var down = new Button { Content = new FontIcon { Glyph = "", FontSize = 12 }, IsEnabled = !isLast, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(4, 0, 0, 0) };
                ToolTipService.SetToolTip(down, "Bajar");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(down, $"Bajar tarea {t.Text}");
                down.Click += (_, _) => { AppState.Config.MoveEnvironmentTask(env.Id, t.Id, false); Render(); };
                Grid.SetColumn(down, 2);

                var del = new Button { Content = new SymbolIcon(Symbol.Delete), Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(4, 0, 0, 0) };
                ToolTipService.SetToolTip(del, "Eliminar");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(del, $"Eliminar tarea {t.Text}");
                del.Click += (_, _) => { AppState.Config.RemoveEnvironmentTask(env.Id, t.Id); Render(); };
                Grid.SetColumn(del, 3);

                var row = new Grid { ColumnSpacing = 2, VerticalAlignment = VerticalAlignment.Center };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (int c = 0; c < 3; c++) row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(check); row.Children.Add(up); row.Children.Add(down); row.Children.Add(del);
                listPanel.Children.Add(row);
            }
        }

        void AddTask()
        {
            var text = input.Text?.Trim() ?? "";
            if (text.Length == 0) return;
            AppState.Config.AddEnvironmentTask(env.Id, text);
            input.Text = "";
            Render();
        }
        addBtn.Click += (_, _) => AddTask();
        input.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) { AddTask(); e.Handled = true; } };

        Render();

        var body = new StackPanel { Spacing = 12, Width = 360 };
        body.Children.Add(addRow);
        body.Children.Add(new ScrollViewer { Content = listPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 320 });

        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = $"Tareas · {env.Name}",
            Content = body,
            CloseButtonText = "Cerrar",
            DefaultButton = ContentDialogButton.Close
        };
        await dlg.ShowAsync();
        BuildEnvList();   // refresca el resumen del módulo (N/total pendientes)
    }

    private static string Summary(FocusEnvironment env)
    {
        var rhythm = PomodoroRhythms.Find(env.PomodoroPreset, AppState.Load().Rhythms);
        var parts = new System.Collections.Generic.List<string> { rhythm?.Name ?? "Pomodoro por defecto" };
        if (env.EnableDoNotDisturb) parts.Add("No molestar");
        if (env.OpenLinksInBrowser) parts.Add("abre enlaces");
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

        int gran = (GranularityBox.SelectedItem is ComboBoxItem gi && gi.Tag is string gt && int.TryParse(gt, out var gm)) ? gm : 60;
        var r3 = AppState.Config.SetGranularity(gran);

        var r4 = AppState.Config.SetShowDayPreviewOnFocusStart(DayPreviewToggle.IsOn);

        int preAlert = (DefaultPreAlertBox.SelectedItem is ComboBoxItem pi && pi.Tag is string pt && int.TryParse(pt, out var pm)) ? pm : 10;
        var r5 = AppState.Config.SetDefaultPreAlert(preAlert);

        if (ThemeBox.SelectedItem is ComboBoxItem it && it.Tag is string tag)
        {
            var theme = tag switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
            if (this.XamlRoot?.Content is FrameworkElement root)
                root.RequestedTheme = theme;
        }

        SaveStatus.Text = (r1.Success && r2.Success && r3.Success && r4.Success && r5.Success)
            ? "✓ Guardado"
            : $"⚠ {(!r1.Success ? r1.Message : !r2.Success ? r2.Message : !r3.Success ? r3.Message : !r4.Success ? r4.Message : r5.Message)}";
    }

    // ---------- Conexiones con apps externas (#123) ----------

    /// <summary>Abre el modal de DESCUBRIMIENTO; al cerrarlo refresca lo conectado.</summary>
    private async void ConnectionsBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ConnectionsDialog { XamlRoot = this.XamlRoot };
        await dlg.ShowAsync();
        RefreshConnections(AppState.Load());
    }

    /// <summary>
    /// Muestra la gestión inline de las conexiones YA creadas (estilo conectores de
    /// Claude). Si no hay ninguna, un texto tenue invita a añadir desde el modal.
    /// «Creada» = tiene topic, aunque esté pausada.
    /// </summary>
    private void RefreshConnections(Ritmo.Core.Persistence.AppSettings s)
    {
        bool created = !string.IsNullOrWhiteSpace(s.NtfyTopic);
        NtfyManageCard.Visibility = created ? Visibility.Visible : Visibility.Collapsed;
        NoConnectionsText.Visibility = created ? Visibility.Collapsed : Visibility.Visible;
        if (created)
        {
            NtfyEnabledToggle.IsOn = s.NtfyEnabled;
            NtfyServerBox.Text = s.NtfyServerUrl ?? "";
            NtfyTopicBox.Text = s.NtfyTopic ?? "";
            NtfyStatus.Text = "";
        }
    }

    private void NtfyGenBtn_Click(object sender, RoutedEventArgs e)
        => NtfyTopicBox.Text = "ritmo-" + Guid.NewGuid().ToString("N").Substring(0, 10);

    // ---------- Actualizaciones (#124, Fase 3) ----------

    private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatus.Text = "Comprobando…";
        CheckUpdateBtn.IsEnabled = false;
        try
        {
            var latest = await Services.GitHubReleasesService.GetLatestAsync();
            var current = AppVersionInfo.Current;
            if (latest is null)
                UpdateStatus.Text = "No se pudo comprobar (sin conexión o aún no hay versiones publicadas).";
            else if (Ritmo.Core.Updates.ReleaseNotes.CompareVersions(latest.Version, current) > 0)
                UpdateStatus.Text = $"✨ Hay una versión nueva ({latest.Tag}). Se instalará sola al reiniciar (App Installer).";
            else
                UpdateStatus.Text = $"✓ Estás al día (v{current}).";
        }
        catch { UpdateStatus.Text = "⚠ Error al comprobar."; }
        finally { CheckUpdateBtn.IsEnabled = true; }
    }

    /// <summary>Abre la guía visual (carrusel) de cómo conectar el móvil con el topic actual.</summary>
    private async void NtfyGuideBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new NtfyGuideDialog((NtfyTopicBox.Text ?? "").Trim(), (NtfyServerBox.Text ?? "").Trim())
        {
            XamlRoot = this.XamlRoot
        };
        await dlg.ShowAsync();
    }

    private void NtfyCopyBtn_Click(object sender, RoutedEventArgs e)
    {
        var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dp.SetText((NtfyTopicBox.Text ?? "").Trim());
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        NtfyStatus.Text = "Topic copiado.";
    }

    private void NtfySaveBtn_Click(object sender, RoutedEventArgs e)
    {
        var r = AppState.Config.SetNtfy(NtfyEnabledToggle.IsOn, NtfyServerBox.Text, NtfyTopicBox.Text);
        NtfyStatus.Text = r.Success ? "✓ Guardado." : $"⚠ {r.Message}";
        if (r.Success) RefreshConnections(AppState.Load());
    }

    private void NtfyRemoveBtn_Click(object sender, RoutedEventArgs e)
    {
        AppState.Config.SetNtfy(false, null, null);   // sin topic -> deja de ser una conexión creada
        RefreshConnections(AppState.Load());
    }

    private async void NtfyTestBtn_Click(object sender, RoutedEventArgs e)
    {
        var topic = (NtfyTopicBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(topic)) { NtfyStatus.Text = "Pon (o genera) un topic primero."; return; }

        NtfyStatus.Text = "Enviando…";
        NtfyTestBtn.IsEnabled = false;
        try
        {
            var pub = Ritmo.Core.Notifications.NtfyPublish.ForTest(NtfyServerBox.Text, topic);
            bool ok = await Services.NtfyPublisher.PublishAsync(pub);
            NtfyStatus.Text = ok
                ? "✓ Enviado. Revisa el móvil suscrito a ese topic."
                : "⚠ No se pudo enviar (revisa servidor, topic y conexión).";
        }
        catch { NtfyStatus.Text = "⚠ Error al enviar la prueba."; }
        finally { NtfyTestBtn.IsEnabled = true; }
    }

    // ---------- Colores del horario por categoría de bloque (#45/#83) ----------

    private static Windows.UI.Color ParseHex(string hex)
    {
        var h = hex.TrimStart('#');
        return Windows.UI.Color.FromArgb(255,
            Convert.ToByte(h.Substring(0, 2), 16),
            Convert.ToByte(h.Substring(2, 2), 16),
            Convert.ToByte(h.Substring(4, 2), 16));
    }

    /// <summary>Una fila por categoría: etiqueta + muestra que abre nuestra paleta curada.</summary>
    /// <summary>
    /// Sección «Categorías» (#83): una fila por categoría con nombre + chip de concentración,
    /// botón de color (paleta curada) y acciones (reordenar/editar/borrar). El alta va por el
    /// botón "Añadir categoría". Las categorías de sistema («Otro»/«Por definir») no se borran.
    /// </summary>
    private void BuildCategories(Ritmo.Core.Persistence.AppSettings s)
    {
        Services.ScheduleColors.SetCategories(s.Categories);   // que las muestras reflejen lo guardado
        ColorsHost.Children.Clear();

        var ordered = s.Categories.OrderBy(c => c.Order).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var category = ordered[i];
            bool isFirst = i == 0, isLast = i == ordered.Count - 1;

            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });   // nombre
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                        // color
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                        // acciones

            var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            namePanel.Children.Add(new TextBlock { Text = category.Name, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 });
            if (category.IsFocus) namePanel.Children.Add(FocusChip());
            Grid.SetColumn(namePanel, 0);

            var colorBtn = BuildColorButton(category);
            Grid.SetColumn(colorBtn, 1);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            var catId = category.Id;
            actions.Children.Add(IconBtn("", "Subir", !isFirst, () => { AppState.Config.ReorderCategory(catId, true); BuildCategories(AppState.Load()); }));
            actions.Children.Add(IconBtn("", "Bajar", !isLast, () => { AppState.Config.ReorderCategory(catId, false); BuildCategories(AppState.Load()); }));
            actions.Children.Add(IconBtn("", "Editar", true, () => _ = EditCategory(category)));
            actions.Children.Add(IconBtn("", "Borrar", !category.IsSystem, () => _ = ConfirmRemoveCategory(category)));
            Grid.SetColumn(actions, 2);

            row.Children.Add(namePanel); row.Children.Add(colorBtn); row.Children.Add(actions);
            ColorsHost.Children.Add(row);
        }
    }

    /// <summary>Etiqueta visual de que una categoría dispara concentración.</summary>
    private static Border FocusChip() => new()
    {
        Background = new SolidColorBrush(((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color),
        CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 1, 7, 2), VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = "Concentración", FontSize = 10, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) }
    };

    /// <summary>Botón pequeño con icono Fluent (deshabilitado si no aplica).</summary>
    private static Button IconBtn(string glyph, string tip, bool enabled, Action onClick)
    {
        var b = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = 14 },
            Padding = new Thickness(7, 5, 7, 5), MinWidth = 0, IsEnabled = enabled
        };
        ToolTipService.SetToolTip(b, tip);
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>Botón de color de una categoría con flyout de la paleta curada (#45).</summary>
    private Button BuildColorButton(BlockCategory category)
    {
        var current = ((SolidColorBrush)Services.ScheduleColors.For(category.Id)).Color;
        var swatch = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = 28, Height = 22, RadiusX = 4, RadiusY = 4,
            Fill = new SolidColorBrush(current),
            Stroke = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], StrokeThickness = 1
        };
        var btn = new Button
        {
            Padding = new Thickness(6, 4, 6, 4),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8,
                Children = { swatch, new TextBlock { Text = "Color", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.8 } }
            }
        };

        var thisCategoryId = category.Id;
        var flyout = new Flyout();
        string currentHex = $"#{current.R:X2}{current.G:X2}{current.B:X2}";

        var swatches = new Grid { ColumnSpacing = 3, RowSpacing = 3 };
        var cols = Services.SchedulePalette.Columns();
        int nRows = cols.Count > 0 ? cols[0].Count : 0;
        for (int c = 0; c < cols.Count; c++) swatches.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int rr = 0; rr < nRows; rr++) swatches.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var strokeBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
        var accentBrush = new SolidColorBrush(((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color);

        for (int c = 0; c < cols.Count; c++)
            for (int rr = 0; rr < cols[c].Count; rr++)
            {
                var hex = cols[c][rr];
                bool isSel = string.Equals(hex, currentHex, StringComparison.OrdinalIgnoreCase);
                var sw = new Button
                {
                    Width = 26, Height = 22, MinWidth = 0, Padding = new Thickness(0),
                    Background = new SolidColorBrush(ParseHex(hex)),
                    CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(isSel ? 2 : 1),
                    BorderBrush = isSel ? accentBrush : strokeBrush
                };
                ToolTipService.SetToolTip(sw, hex);
                Grid.SetColumn(sw, c); Grid.SetRow(sw, rr);
                var thisHex = hex;
                sw.Click += (_, _) => { AppState.Config.SetKindColor(thisCategoryId, thisHex); flyout.Hide(); BuildCategories(AppState.Load()); };
                swatches.Children.Add(sw);
            }

        var resetBtn = new Button { Content = "Usar por defecto", HorizontalAlignment = HorizontalAlignment.Stretch };
        resetBtn.Click += (_, _) => { AppState.Config.SetKindColor(thisCategoryId, null); flyout.Hide(); BuildCategories(AppState.Load()); };

        flyout.Content = new StackPanel { Spacing = 10, Children = { swatches, resetBtn } };
        btn.Flyout = flyout;
        return btn;
    }

    private async void AddCategoryBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CategoryDialog { XamlRoot = this.XamlRoot };
        dlg.LoadDefaults();
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var res = AppState.Config.AddCategory(dlg.CategoryName, dlg.SelectedColorHex, dlg.IsFocus);
        if (!res.Success) await CategoryError(res.Message);
        BuildCategories(AppState.Load());
    }

    private async Task EditCategory(BlockCategory cat)
    {
        var dlg = new CategoryDialog { XamlRoot = this.XamlRoot };
        dlg.LoadFrom(cat);
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var res = AppState.Config.UpdateCategory(cat.Id, dlg.CategoryName, dlg.SelectedColorHex, dlg.IsFocus);
        if (!res.Success) await CategoryError(res.Message);
        BuildCategories(AppState.Load());
        BuildKindEnvMap();   // cambiar el focus altera qué categorías mapean a un entorno
    }

    private async Task ConfirmRemoveCategory(BlockCategory cat)
    {
        var confirm = new ContentDialog
        {
            XamlRoot = this.XamlRoot, Title = "Borrar categoría",
            Content = $"¿Borrar «{cat.Name}»? Los bloques que la usan pasarán a «Otro».",
            PrimaryButtonText = "Borrar", CloseButtonText = "Cancelar", DefaultButton = ContentDialogButton.Close
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        var res = AppState.Config.RemoveCategory(cat.Id);
        if (!res.Success) await CategoryError(res.Message);
        BuildCategories(AppState.Load());
        BuildKindEnvMap();
    }

    private async Task CategoryError(string msg)
        => await new ContentDialog { XamlRoot = this.XamlRoot, Title = "Categorías", Content = msg, CloseButtonText = "Vale" }.ShowAsync();

    /// <summary>Selecciona el item del combo cuyo Tag coincide (cae al primero si no hay match).</summary>
    private static void SelectComboByTag(ComboBox box, string tag)
    {
        for (int i = 0; i < box.Items.Count; i++)
            if (box.Items[i] is ComboBoxItem it && (string)it.Tag == tag) { box.SelectedIndex = i; return; }
        box.SelectedIndex = 0;
    }
}
