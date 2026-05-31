using System;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

/// <summary>Comandos CRUD de categorías de bloque + plantillas (#83, Fase 2).</summary>
public class CategoryCommandsTests
{
    private static (ConfigurationService svc, ISettingsStore store) New()
    {
        var store = new InMemorySettingsStore();
        return (new ConfigurationService(store), store);
    }

    [Fact]
    public void AddCategory_genera_slug_y_persiste()
    {
        var (svc, store) = New();
        var r = svc.AddCategory("Lectura crítica", "#AABBCC", isFocus: true);
        Assert.True(r.Success);
        Assert.Equal("lectura-critica", r.Message);
        var cat = store.Load().Category("lectura-critica");
        Assert.NotNull(cat);
        Assert.True(cat!.IsFocus);
        Assert.Equal("#AABBCC", cat.ColorHex);
    }

    [Fact]
    public void AddCategory_valida_nombre_y_color()
    {
        var (svc, _) = New();
        Assert.False(svc.AddCategory("  ", "#AABBCC", false).Success);
        Assert.False(svc.AddCategory("X", "no-color", false).Success);
    }

    [Fact]
    public void UpdateCategory_cambia_datos_pero_no_id()
    {
        var (svc, store) = New();
        var id = svc.AddCategory("Foco", "#111111", false).Message;
        Assert.True(svc.UpdateCategory(id, "Foco profundo", "#222222", true).Success);
        var cat = store.Load().Category(id);
        Assert.Equal("Foco profundo", cat!.Name);
        Assert.True(cat.IsFocus);
        Assert.Equal("#222222", cat.ColorHex);
        Assert.False(svc.UpdateCategory("no-existe", "X", "#333333", false).Success);
    }

    [Fact]
    public void RemoveCategory_de_sistema_falla()
    {
        var (svc, _) = New();
        Assert.False(svc.RemoveCategory(CategoryIds.Other).Success);
        Assert.False(svc.RemoveCategory(CategoryIds.Undecided).Success);
    }

    [Fact]
    public void RemoveCategory_reasigna_los_bloques_a_Otro()
    {
        var (svc, store) = New();
        var id = svc.AddCategory("Proyecto X", "#112233", true).Message;
        var s = store.Load();
        store.Save(s with
        {
            Schedule = new WeeklySchedule
            {
                Sessions = [ new StudySession {
                    Title = "t", Day = DayOfWeek.Monday, Start = new TimeOnly(9, 0),
                    Duration = TimeSpan.FromHours(1), CategoryId = id } ]
            }
        });

        Assert.True(svc.RemoveCategory(id).Success);
        var after = store.Load();
        Assert.DoesNotContain(after.Categories, c => c.Id == id);
        Assert.Equal(CategoryIds.Other, after.Schedule.Sessions.Single().CategoryId);
    }

    [Fact]
    public void ReorderCategory_intercambia_posiciones()
    {
        var (svc, store) = New();
        var a = svc.AddCategory("AAA", "#111111", false).Message;
        var b = svc.AddCategory("BBB", "#222222", false).Message;
        // a queda justo antes que b; subir b los intercambia
        var oa = store.Load().Category(a)!.Order;
        var ob = store.Load().Category(b)!.Order;
        Assert.True(oa < ob);
        Assert.True(svc.ReorderCategory(b, up: true).Success);
        Assert.True(store.Load().Category(b)!.Order < store.Load().Category(a)!.Order);
    }

    [Fact]
    public void SeedTemplate_siembra_y_marca_onboarding()
    {
        var (svc, store) = New();
        Assert.True(svc.SeedTemplate(CategoryDefaults.Study).Success);
        var s = store.Load();
        Assert.True(s.OnboardingCompleted);
        Assert.Contains(s.Categories, c => c.Name == "Lectura");
        Assert.NotNull(s.Category(CategoryIds.Other));   // sistema siempre presente
    }
}
