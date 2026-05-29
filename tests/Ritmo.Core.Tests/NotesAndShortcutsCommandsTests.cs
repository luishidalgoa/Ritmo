using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

public class NotesAndShortcutsCommandsTests
{
    private static (ConfigurationService svc, ISettingsStore store) New()
    {
        var store = new InMemorySettingsStore();
        return (new ConfigurationService(store), store);
    }

    [Fact]
    public void AddNote_crea_con_orden_incremental()
    {
        var (svc, store) = New();
        svc.AddNote("Primera", "a");
        svc.AddNote("Segunda", "b");
        var notes = store.Load().Notes;
        Assert.Equal(2, notes.Count);
        Assert.True(notes[1].Order > notes[0].Order);
    }

    [Fact]
    public void AddNote_sin_titulo_falla()
    {
        var (svc, _) = New();
        Assert.False(svc.AddNote("  ", "x").Success);
    }

    [Fact]
    public void UpdateNote_cambia_titulo_y_contenido()
    {
        var (svc, store) = New();
        var id = svc.AddNote("Vieja", "x").Message;   // AddNote devuelve el id en Message
        Assert.True(svc.UpdateNote(id, "Nueva", "y").Success);
        var note = store.Load().Notes.Single();
        Assert.Equal("Nueva", note.Title);
        Assert.Equal("y", note.Content);
    }

    [Fact]
    public void RemoveNote_borra_por_id()
    {
        var (svc, store) = New();
        var id = svc.AddNote("Borrar", "x").Message;
        Assert.True(svc.RemoveNote(id).Success);
        Assert.Empty(store.Load().Notes);
    }

    [Fact]
    public void RemoveNote_inexistente_falla()
    {
        var (svc, _) = New();
        Assert.False(svc.RemoveNote("nope").Success);
    }

    [Fact]
    public void AddShortcut_y_RemoveShortcut()
    {
        var (svc, store) = New();
        Assert.True(svc.AddShortcut("Campus", "https://campus.zbrain.es").Success);
        Assert.True(svc.AddShortcut("BOE", "https://boe.es").Success);
        Assert.Equal(2, store.Load().ViewConfig.Shortcuts.Count);

        Assert.True(svc.RemoveShortcut(0).Success);
        var sc = store.Load().ViewConfig.Shortcuts;
        Assert.Single(sc);
        Assert.Equal("BOE", sc[0].Title);
    }

    [Fact]
    public void AddShortcut_valida_titulo_y_url()
    {
        var (svc, _) = New();
        Assert.False(svc.AddShortcut("", "https://x").Success);
        Assert.False(svc.AddShortcut("X", "  ").Success);
    }

    [Fact]
    public void RemoveShortcut_fuera_de_rango_falla()
    {
        var (svc, _) = New();
        Assert.False(svc.RemoveShortcut(0).Success);
    }
}
