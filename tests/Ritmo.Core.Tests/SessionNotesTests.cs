using System.Linq;
using Ritmo.Core.Commands;

namespace Ritmo.Core.Tests;

public class SessionNotesTests
{
    [Fact]
    public void AddNote_con_sessionTitle_la_asocia_a_la_sesion()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);

        svc.AddNote("Repasar tema 5", "**ojo** con el art. 14", sessionTitle: "Legislación (B.I)");
        svc.AddNote("Nota general", "suelta");   // sin sesión

        var notes = store.Load().Notes;
        Assert.Equal(2, notes.Count);
        Assert.Equal("Legislación (B.I)", notes.Single(n => n.Title == "Repasar tema 5").SessionTitle);
        Assert.Null(notes.Single(n => n.Title == "Nota general").SessionTitle);
    }

    [Fact]
    public void UpdateNote_conserva_la_asociacion()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        var id = svc.AddNote("T", "c", sessionTitle: "Técnico").Message;

        Assert.True(svc.UpdateNote(id, "T2", "c2").Success);
        var n = store.Load().Notes.Single();
        Assert.Equal("T2", n.Title);
        Assert.Equal("Técnico", n.SessionTitle);   // la asociación se mantiene
    }

    [Fact]
    public void Asociacion_sobrevive_export_import()
    {
        var store = new InMemorySettingsStore();
        var svc = new ConfigurationService(store);
        svc.AddNote("Post-it", "x", sessionTitle: "Tests del tema");

        var json = svc.ExportJson();
        var other = new ConfigurationService(new InMemorySettingsStore());
        Assert.True(other.ImportJson(json).Success);
        Assert.Equal("Tests del tema", other.GetSettings().Notes.Single().SessionTitle);
    }
}
