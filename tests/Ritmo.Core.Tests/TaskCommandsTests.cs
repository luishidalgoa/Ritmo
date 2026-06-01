using System;
using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Focus;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

public class TaskCommandsTests
{
    private static (ConfigurationService svc, ISettingsStore store) New()
    {
        var store = new InMemorySettingsStore();
        return (new ConfigurationService(store), store);
    }

    [Fact]
    public void Crea_bloque_y_devuelve_id()
    {
        var (svc, store) = New();
        var r = svc.AddTaskBlock("Compras");
        Assert.True(r.Success);
        var blocks = store.Load().TaskBlocks;
        Assert.Single(blocks);
        Assert.Equal("Compras", blocks[0].Name);
        Assert.Equal(r.Message, blocks[0].Id);
    }

    [Fact]
    public void Bloque_sin_nombre_falla()
        => Assert.False(New().svc.AddTaskBlock("   ").Success);

    [Fact]
    public void Anade_tareas_con_orden_incremental()
    {
        var (svc, store) = New();
        var bid = svc.AddTaskBlock("Lista").Message;
        svc.AddTask(bid, "A");
        svc.AddTask(bid, "B");
        var tasks = store.Load().Tasks.Where(t => t.BlockId == bid).OrderBy(t => t.Order).ToList();
        Assert.Equal(2, tasks.Count);
        Assert.Equal("A", tasks[0].Text);
        Assert.Equal(0, tasks[0].Order);
        Assert.Equal(1, tasks[1].Order);
    }

    [Fact]
    public void Tarea_en_bloque_inexistente_falla()
        => Assert.False(New().svc.AddTask("no-existe", "X").Success);

    [Fact]
    public void Toggle_marca_y_desmarca()
    {
        var (svc, store) = New();
        var bid = svc.AddTaskBlock("L").Message;
        var tid = svc.AddTask(bid, "Tarea").Message;
        svc.ToggleTask(tid);
        Assert.True(store.Load().Tasks.First(t => t.Id == tid).Done);
        svc.ToggleTask(tid);
        Assert.False(store.Load().Tasks.First(t => t.Id == tid).Done);
    }

    [Fact]
    public void Renombrar_y_notas_y_fecha()
    {
        var (svc, store) = New();
        var bid = svc.AddTaskBlock("L").Message;
        var tid = svc.AddTask(bid, "Tarea").Message;
        svc.RenameTask(tid, "Nueva");
        svc.SetTaskNotes(tid, "detalle");
        svc.SetTaskDueDate(tid, new DateOnly(2026, 6, 10));
        var t = store.Load().Tasks.First(x => x.Id == tid);
        Assert.Equal("Nueva", t.Text);
        Assert.Equal("detalle", t.Notes);
        Assert.Equal(new DateOnly(2026, 6, 10), t.DueDate);
    }

    [Fact]
    public void Borrar_bloque_borra_sus_tareas()
    {
        var (svc, store) = New();
        var bid = svc.AddTaskBlock("L").Message;
        svc.AddTask(bid, "A");
        svc.AddTask(bid, "B");
        svc.RemoveTaskBlock(bid);
        Assert.Empty(store.Load().TaskBlocks);
        Assert.Empty(store.Load().Tasks);
    }

    [Fact]
    public void Mover_tarea_reordena_dentro_del_bloque()
    {
        var (svc, store) = New();
        var bid = svc.AddTaskBlock("L").Message;
        var a = svc.AddTask(bid, "A").Message;
        var b = svc.AddTask(bid, "B").Message;
        svc.MoveTask(b, up: true);   // B sube a la posición 0
        var ordered = store.Load().Tasks.Where(t => t.BlockId == bid).OrderBy(t => t.Order).ToList();
        Assert.Equal("B", ordered[0].Text);
        Assert.Equal("A", ordered[1].Text);
    }

    [Fact]
    public void Mover_bloque_reordena()
    {
        var (svc, store) = New();
        var a = svc.AddTaskBlock("A").Message;
        var b = svc.AddTaskBlock("B").Message;
        svc.MoveTaskBlock(b, up: true);
        var ordered = store.Load().TaskBlocks.OrderBy(x => x.Order).ToList();
        Assert.Equal("B", ordered[0].Name);
        Assert.Equal("A", ordered[1].Name);
    }

    [Fact]
    public void Vincular_y_desvincular_entorno()
    {
        var (svc, store) = New();
        var bid = svc.AddTaskBlock("L").Message;
        svc.SetTaskBlockEnvironment(bid, "env-1");
        Assert.Equal("env-1", store.Load().TaskBlocks.First(b => b.Id == bid).EnvironmentId);
        svc.SetTaskBlockEnvironment(bid, null);
        Assert.Null(store.Load().TaskBlocks.First(b => b.Id == bid).EnvironmentId);
    }

    [Fact]
    public void EnsureEnvironmentTaskBlock_crea_vincula_y_migra_tareas_viejas()
    {
        var store = new InMemorySettingsStore();
        var env = new FocusEnvironment
        {
            Id = "e1", Name = "Trabajo",
            Tasks = new[]
            {
                new EnvironmentTask { Id = "t1", Text = "Comprar", Order = 0 },
                new EnvironmentTask { Id = "t2", Text = "Llamar", Done = true, Order = 1 }
            }
        };
        store.Save(AppSettings.Default with { FocusEnvironments = new[] { env } });
        var svc = new ConfigurationService(store);

        var r = svc.EnsureEnvironmentTaskBlock("e1", "Trabajo");
        Assert.True(r.Success);
        var s = store.Load();
        var block = s.TaskBlocks.Single();
        Assert.Equal("e1", block.EnvironmentId);
        Assert.Equal(2, s.Tasks.Count(t => t.BlockId == block.Id));
        Assert.Contains(s.Tasks, t => t.Text == "Comprar" && !t.Done);
        Assert.Contains(s.Tasks, t => t.Text == "Llamar" && t.Done);
        Assert.Empty(s.FocusEnvironments.Single().Tasks);   // las viejas se vaciaron (migradas)

        // Idempotente: segunda llamada devuelve el MISMO bloque, sin duplicar.
        var r2 = svc.EnsureEnvironmentTaskBlock("e1", "Trabajo");
        Assert.Equal(block.Id, r2.Message);
        Assert.Single(store.Load().TaskBlocks);
    }

    [Fact]
    public void Persisten_tras_ida_y_vuelta_DTO()
    {
        var (svc, store) = New();
        var bid = svc.AddTaskBlock("Lista", "#FF0000", "env-9").Message;
        var tid = svc.AddTask(bid, "Tarea", sessionKey: "Trabajo|cat|09:00|01:00:00").Message;
        svc.SetTaskDueDate(tid, new DateOnly(2026, 6, 1));

        // Serializa a JSON y vuelve (cubre el mapeo DTO de ida y vuelta).
        var json = SettingsJson.Serialize(store.Load());
        var round = SettingsJson.Deserialize(json);

        var block = round.TaskBlocks.Single();
        Assert.Equal("Lista", block.Name);
        Assert.Equal("#FF0000", block.ColorHex);
        Assert.Equal("env-9", block.EnvironmentId);
        var task = round.Tasks.Single();
        Assert.Equal("Tarea", task.Text);
        Assert.Equal("Trabajo|cat|09:00|01:00:00", task.SessionKey);
        Assert.Equal(new DateOnly(2026, 6, 1), task.DueDate);
    }
}
