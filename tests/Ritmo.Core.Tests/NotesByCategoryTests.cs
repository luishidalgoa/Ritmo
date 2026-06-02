using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Model;

namespace Ritmo.Core.Tests;

/// <summary>Notas asignadas a una CATEGORÍA de sesión (#153).</summary>
public class NotesByCategoryTests
{
    // ---------- StudyNote.AppliesTo / IsGeneral (lógica pura) ----------

    [Fact]
    public void Nota_por_categoria_aplica_a_toda_sesion_de_esa_categoria()
    {
        var note = new StudyNote { Id = "1", Title = "T", CategoryId = "Tecnico" };
        Assert.True(note.AppliesTo("Cualquier sesión", "Tecnico"));
        Assert.True(note.AppliesTo("Otra sesión distinta", "tecnico"));   // case-insensitive
        Assert.False(note.AppliesTo("Una sesión", "Legislacion"));
        Assert.False(note.IsGeneral);
    }

    [Fact]
    public void Nota_por_titulo_solo_aplica_a_esa_sesion()
    {
        var note = new StudyNote { Id = "1", Title = "T", SessionTitle = "Tests del tema" };
        Assert.True(note.AppliesTo("Tests del tema", "Tecnico"));
        Assert.False(note.AppliesTo("Otra sesión", "Tecnico"));
        Assert.False(note.IsGeneral);
    }

    [Fact]
    public void Nota_general_no_aplica_a_ninguna_sesion_concreta()
    {
        var note = new StudyNote { Id = "1", Title = "T" };
        Assert.True(note.IsGeneral);
        Assert.False(note.AppliesTo("Sesión", "Tecnico"));
    }

    [Fact]
    public void La_categoria_tiene_prioridad_sobre_el_titulo()
    {
        // Si por lo que fuera hubiera ambos, manda la categoría.
        var note = new StudyNote { Id = "1", Title = "T", SessionTitle = "Solo esta", CategoryId = "Tecnico" };
        Assert.True(note.AppliesTo("Cualquier otra", "Tecnico"));
        Assert.False(note.AppliesTo("Solo esta", "Legislacion"));
    }

    // ---------- ConfigurationService.AddNote / UpdateNote ----------

    [Fact]
    public void AddNote_con_categoryId_la_asocia_a_la_categoria_y_no_al_titulo()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);

        svc.AddNote("Recordatorio", "vale para todas", sessionTitle: "Da igual", categoryId: "Tecnico");

        var n = store.Load().Notes.Single();
        Assert.Equal("Tecnico", n.CategoryId);
        Assert.Null(n.SessionTitle);   // la categoría manda: el título se descarta
    }

    [Fact]
    public void UpdateNote_con_setScope_reasigna_de_titulo_a_categoria()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        var id = svc.AddNote("T", "c", sessionTitle: "Técnico").Message;

        Assert.True(svc.UpdateNote(id, "T", "c", setScope: true, categoryId: "Tecnico").Success);

        var n = store.Load().Notes.Single();
        Assert.Equal("Tecnico", n.CategoryId);
        Assert.Null(n.SessionTitle);
    }

    [Fact]
    public void UpdateNote_sin_setScope_conserva_el_ambito()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        var id = svc.AddNote("T", "c", categoryId: "Tecnico").Message;

        Assert.True(svc.UpdateNote(id, "T2", "c2").Success);   // sin setScope

        var n = store.Load().Notes.Single();
        Assert.Equal("T2", n.Title);
        Assert.Equal("Tecnico", n.CategoryId);   // se mantiene
    }

    [Fact]
    public void UpdateNote_con_setScope_a_general_limpia_ambito()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        var id = svc.AddNote("T", "c", categoryId: "Tecnico").Message;

        Assert.True(svc.UpdateNote(id, "T", "c", setScope: true).Success);   // sin título ni categoría

        var n = store.Load().Notes.Single();
        Assert.True(n.IsGeneral);
    }

    [Fact]
    public void CategoryId_sobrevive_export_import()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.AddNote("Post-it de tipo", "x", categoryId: "Tecnico");

        var json = svc.ExportJson();
        var other = new ConfigurationService(new InMemorySettingsStore());
        Assert.True(other.ImportJson(json).Success);
        Assert.Equal("Tecnico", other.GetSettings().Notes.Single().CategoryId);
    }
}
