using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Model;

namespace Ritmo_App.Dialogs;

/// <summary>Diálogo para crear o editar una nota fijada (#55).</summary>
public sealed partial class NoteDialog : ContentDialog
{
    /// <summary>Id de la nota en edición (null = nota nueva).</summary>
    public string? NoteId { get; private set; }

    public NoteDialog()
    {
        InitializeComponent();
    }

    public void LoadFrom(StudyNote note)
    {
        NoteId = note.Id;
        TitleBox.Text = note.Title;
        ContentBox.Text = note.Content;
    }

    public string TitleText => TitleBox.Text.Trim();
    public string ContentText => ContentBox.Text;
}
