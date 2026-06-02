using System.Linq;
using Ritmo.Core.Commands;
using Ritmo.Core.Model;
using Ritmo.Core.Persistence;

namespace Ritmo.Core.Tests;

public class TaskTombstoneTests
{
    private static (ConfigurationService Svc, InMemorySettingsStore Store) Setup(TaskBlock block, TaskItem task)
    {
        var store = new InMemorySettingsStore();
        store.Save(AppSettings.Default with { TaskBlocks = [block], Tasks = [task] });
        return (new ConfigurationService(store), store);
    }

    [Fact]
    public void RemoveTask_sincronizada_deja_lapida()
    {
        var block = new TaskBlock { Id = "b1", Name = "Lista", Provider = "google", ExternalId = "list-1" };
        var task = new TaskItem { Id = "t1", BlockId = "b1", Text = "x", ExternalId = "rt-1" };
        var (svc, store) = Setup(block, task);

        Assert.True(svc.RemoveTask("t1").Success);
        var s = store.Load();
        Assert.Empty(s.Tasks);
        var tomb = Assert.Single(s.TaskTombstones);
        Assert.Equal("google", tomb.Provider);
        Assert.Equal("list-1", tomb.ListId);
        Assert.Equal("rt-1", tomb.TaskId);
    }

    [Fact]
    public void RemoveTask_local_no_deja_lapida()
    {
        var block = new TaskBlock { Id = "b1", Name = "Lista" };          // bloque sin proveedor
        var task = new TaskItem { Id = "t1", BlockId = "b1", Text = "x" }; // tarea sin ExternalId
        var (svc, store) = Setup(block, task);

        svc.RemoveTask("t1");
        Assert.Empty(store.Load().TaskTombstones);
    }

    [Fact]
    public void RemoveTask_sincronizada_pero_bloque_sin_vincular_no_deja_lapida()
    {
        // La tarea tiene ExternalId pero su bloque ya no está vinculado (sin Provider): no hay a quién borrar.
        var block = new TaskBlock { Id = "b1", Name = "Lista" };
        var task = new TaskItem { Id = "t1", BlockId = "b1", Text = "x", ExternalId = "rt-1" };
        var (svc, store) = Setup(block, task);

        svc.RemoveTask("t1");
        Assert.Empty(store.Load().TaskTombstones);
    }

    [Fact]
    public void Lapida_sobrevive_export_import()
    {
        var block = new TaskBlock { Id = "b1", Name = "Lista", Provider = "google", ExternalId = "list-9" };
        var task = new TaskItem { Id = "t1", BlockId = "b1", Text = "x", ExternalId = "rt-9" };
        var (svc, store) = Setup(block, task);
        svc.RemoveTask("t1");

        var json = svc.ExportJson();
        var other = new ConfigurationService(new InMemorySettingsStore());
        Assert.True(other.ImportJson(json).Success);
        Assert.Single(other.GetSettings().TaskTombstones);
    }
}
