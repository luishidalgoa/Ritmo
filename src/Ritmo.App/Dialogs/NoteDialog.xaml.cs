using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Model;
using Ritmo_App.Services;

namespace Ritmo_App.Dialogs;

/// <summary>
/// Diálogo para crear o editar una nota fijada (#55). Opcionalmente (#153) permite elegir el
/// ÁMBITO de la nota: general, solo una sesión (por título) o una CATEGORÍA de bloque (aplica a
/// todas las sesiones de ese tipo). El selector solo aparece si el llamante invoca <see cref="EnableScope"/>.
/// </summary>
public sealed partial class NoteDialog : ContentDialog
{
    /// <summary>Id de la nota en edición (null = nota nueva).</summary>
    public string? NoteId { get; private set; }

    private bool _scopeShown;
    private string? _sessionTitle;   // contexto de sesión (si se creó/edita desde una sesión)

    public NoteDialog()
    {
        InitializeComponent();
        // Localización en caliente (#48/#153): el diálogo se construye por código en varios sitios.
        Title = Loc.Pick("Nota", "Note");
        PrimaryButtonText = Loc.Pick("Guardar", "Save");
        SecondaryButtonText = Loc.Pick("Cancelar", "Cancel");
        TitleBox.Header = Loc.Pick("Título", "Title");
        ContentBox.Header = Loc.Pick("Contenido", "Content");
    }

    public void LoadFrom(StudyNote note)
    {
        NoteId = note.Id;
        TitleBox.Text = note.Title;
        ContentBox.Text = note.Content;
        if (_scopeShown) SelectScopeFor(note);
    }

    /// <summary>
    /// Muestra el selector de ámbito (#153): General · [Solo esta sesión] · una opción por categoría.
    /// <paramref name="sessionTitle"/> = sesión de contexto (si se crea desde una sesión concreta).
    /// </summary>
    public void EnableScope(IReadOnlyList<BlockCategory> categories, string? sessionTitle = null)
    {
        _sessionTitle = string.IsNullOrWhiteSpace(sessionTitle) ? null : sessionTitle.Trim();
        ScopeBox.Header = Loc.Pick("Asignar a", "Assign to");
        ScopeBox.Items.Clear();
        ScopeBox.Items.Add(new ComboBoxItem { Content = Loc.Pick("General (siempre visible en «Hoy»)", "General (always visible in «Today»)"), Tag = "general" });
        if (_sessionTitle is not null)
            ScopeBox.Items.Add(new ComboBoxItem { Content = $"{Loc.Pick("Solo esta sesión", "Only this session")} · {_sessionTitle}", Tag = "session" });
        foreach (var c in categories.Where(c => c.Id != CategoryIds.Undecided))
            ScopeBox.Items.Add(new ComboBoxItem { Content = $"{Loc.Pick("Categoría", "Category")} · {c.Name}", Tag = $"cat:{c.Id}" });

        ScopeBox.SelectedIndex = _sessionTitle is not null ? 1 : 0;   // por defecto: esta sesión si hay, si no general
        ScopeBox.Visibility = Visibility.Visible;
        ScopeHint.Text = Loc.Pick(
            "Las notas de una categoría se muestran en todas las sesiones de ese tipo.",
            "Category notes show on every session of that type.");
        ScopeHint.Visibility = Visibility.Visible;
        _scopeShown = true;
    }

    /// <summary>Selecciona en el combo el ámbito que corresponde a la nota dada.</summary>
    private void SelectScopeFor(StudyNote note)
    {
        string targetTag =
            !string.IsNullOrWhiteSpace(note.CategoryId) ? $"cat:{note.CategoryId}"
            : !string.IsNullOrWhiteSpace(note.SessionTitle) ? "session"
            : "general";

        // Si la nota es de una sesión por título y no había contexto de sesión, añade su opción.
        if (targetTag == "session")
        {
            _sessionTitle = note.SessionTitle!.Trim();
            if (!ScopeBox.Items.OfType<ComboBoxItem>().Any(it => (string)it.Tag == "session"))
                ScopeBox.Items.Insert(1, new ComboBoxItem { Content = $"{Loc.Pick("Solo esta sesión", "Only this session")} · {_sessionTitle}", Tag = "session" });
        }

        foreach (ComboBoxItem it in ScopeBox.Items.OfType<ComboBoxItem>())
            if ((string)it.Tag == targetTag) { ScopeBox.SelectedItem = it; return; }
        ScopeBox.SelectedIndex = 0;   // general por defecto si la categoría ya no existe
    }

    public string TitleText => TitleBox.Text.Trim();
    public string ContentText => ContentBox.Text;

    /// <summary>¿Se mostró el selector de ámbito? (para que el llamante decida si reasigna ámbito).</summary>
    public bool ScopeWasShown => _scopeShown;

    private string ScopeTag => (ScopeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "general";

    /// <summary>Categoría elegida (null si no es por categoría). #153</summary>
    public string? SelectedCategoryId =>
        _scopeShown && ScopeTag.StartsWith("cat:") ? ScopeTag["cat:".Length..] : null;

    /// <summary>Título de sesión elegido (null si no es por sesión). #153</summary>
    public string? SelectedSessionTitle =>
        !_scopeShown ? _sessionTitle : (ScopeTag == "session" ? _sessionTitle : null);
}
