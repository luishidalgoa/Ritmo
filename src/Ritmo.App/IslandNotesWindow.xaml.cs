using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ritmo.Core.Model;
using Ritmo_App.Services;
using Windows.Graphics;

namespace Ritmo_App;

/// <summary>
/// Ventana-modal de notas que se superpone a la ISLA flotante (#153b): permite ver/editar las notas
/// de la sesión activa —o generales si no hay sesión— SIN salir del modo concentración (antes el atajo
/// de la isla devolvía a la app y rompía el foco). Siempre encima; al cerrarla, la isla sigue ahí.
/// </summary>
public sealed partial class IslandNotesWindow : Window
{
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);

    private readonly string? _sessionTitle;   // sesión activa (null = notas generales)
    private readonly string? _categoryId;
    private string? _editingId;               // nota en edición (null = nota nueva)

    public IslandNotesWindow(string? sessionTitle, string? categoryId)
    {
        InitializeComponent();
        _sessionTitle = string.IsNullOrWhiteSpace(sessionTitle) ? null : sessionTitle.Trim();
        _categoryId = string.IsNullOrWhiteSpace(categoryId) ? null : categoryId.Trim();

        Title = "Notas — Ritmo";
        AppWindow.IsShownInSwitchers = false;
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.IsAlwaysOnTop = true;
            p.IsMaximizable = false;
            p.IsMinimizable = false;
            p.SetBorderAndTitleBar(true, true);
        }

        double scale = Scale();
        AppWindow.Resize(new SizeInt32((int)(420 * scale), (int)(560 * scale)));
        CenterOnScreen();

        if (_sessionTitle is not null)
        {
            ScopeText.Text = "Notas · " + _sessionTitle;
            ScopeHint.Text = "Se asocian a esta sesión.";
        }
        else
        {
            ScopeText.Text = "Notas generales";
            ScopeHint.Text = "No hay sesión activa: se guardan como notas generales (visibles en «Hoy»).";
        }

        BuildList();
    }

    private double Scale()
    {
        try { var s = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0; return s <= 0 ? 1.0 : s; }
        catch { return 1.0; }
    }

    private void CenterOnScreen()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var wa = area.WorkArea;
        var sz = AppWindow.Size;
        AppWindow.Move(new PointInt32(wa.X + (wa.Width - sz.Width) / 2, wa.Y + (wa.Height - sz.Height) / 2));
    }

    // ---------- Editor ----------

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text.Trim();
        if (title.Length == 0) { StatusText.Text = "Pon un título."; return; }
        var content = ContentBox.Text;

        if (_editingId is null)
            AppState.Config.AddNote(title, content, sessionTitle: _sessionTitle);   // null => nota general
        else
            AppState.Config.UpdateNote(_editingId, title, content);                 // conserva su ámbito

        ResetEditor();
        StatusText.Text = "✓ Guardada";
        BuildList();
    }

    private void NewBtn_Click(object sender, RoutedEventArgs e) => ResetEditor();

    private void ResetEditor()
    {
        _editingId = null;
        TitleBox.Text = "";
        ContentBox.Text = "";
        SaveBtnText.Text = "Guardar nota";
        NewBtn.Visibility = Visibility.Collapsed;
    }

    private void LoadForEdit(StudyNote note)
    {
        _editingId = note.Id;
        TitleBox.Text = note.Title;
        ContentBox.Text = note.Content;
        SaveBtnText.Text = "Actualizar";
        NewBtn.Visibility = Visibility.Visible;
        StatusText.Text = "";
        TitleBox.Focus(FocusState.Programmatic);
    }

    // ---------- Lista de notas existentes ----------

    private void BuildList()
    {
        NotesList.Children.Clear();
        var notes = AppState.Load().Notes
            .Where(n => n.AppliesTo(_sessionTitle, _categoryId) || n.IsGeneral)
            .OrderBy(n => n.IsGeneral ? 1 : 0)
            .ThenBy(n => n.Order)
            .ToList();

        if (notes.Count == 0)
        {
            NotesList.Children.Add(new TextBlock
            {
                Text = "Aún no hay notas. Escribe una arriba.",
                Opacity = 0.5, FontSize = 12, TextWrapping = TextWrapping.Wrap
            });
            return;
        }
        foreach (var n in notes) NotesList.Children.Add(NoteCard(n));
    }

    private FrameworkElement NoteCard(StudyNote note)
    {
        var texts = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        if (note.IsGeneral)
            texts.Children.Add(new TextBlock { Text = "General", FontSize = 10, Opacity = 0.5, FontWeight = FontWeights.SemiBold });
        texts.Children.Add(new TextBlock { Text = note.Title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(note.Content))
            texts.Children.Add(new TextBlock
            {
                Text = note.Content, Opacity = 0.7, FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, TextWrapping = TextWrapping.Wrap
            });
        Grid.SetColumn(texts, 0);

        var edit = IconButton(Symbol.Edit, "Editar", () => LoadForEdit(note));
        Grid.SetColumn(edit, 1);
        var del = IconButton(Symbol.Delete, "Borrar", () =>
        {
            AppState.Config.RemoveNote(note.Id);
            if (_editingId == note.Id) ResetEditor();
            BuildList();
        });
        Grid.SetColumn(del, 2);

        var grid = new Grid { ColumnSpacing = 4 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(texts); grid.Children.Add(edit); grid.Children.Add(del);

        return new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 6, 8), Child = grid
        };
    }

    private static Button IconButton(Symbol symbol, string tip, Action onClick)
    {
        var b = new Button
        {
            Content = new SymbolIcon(symbol) { Width = 16, Height = 16 },
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0), Padding = new Thickness(6), MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Top
        };
        ToolTipService.SetToolTip(b, tip);
        b.Click += (_, _) => onClick();
        return b;
    }
}
