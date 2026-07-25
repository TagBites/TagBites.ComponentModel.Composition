using TagBites.ComponentModel.Composition.Tests.Models;
using Xunit;

namespace TagBites.ComponentModel.Composition.Tests;

public class ExportComponentManagerTests
{
    [Fact]
    public void ExportDiscovery()
    {
        var manager = CreateManager();
        var instances = manager.GetExportInstances<ITestService>().ToList();

        Assert.Contains(instances, x => x is AlphaService);
        Assert.Contains(instances, x => x is BetaService);
    }

    [Fact]
    public void NamedContract()
    {
        var manager = CreateManager();
        var instances = manager.GetExportInstances<ITestService>("Named").ToList();

        Assert.Single(instances);
        Assert.IsType<NamedService>(instances[0]);
    }

    [Fact]
    public void DefaultContractExcludesNamedExports()
    {
        var manager = CreateManager();
        var instances = manager.GetExportInstances<ITestService>().ToList();

        Assert.DoesNotContain(instances, x => x is NamedService);
    }

    [Fact]
    public void DuplicateUriSkipsCurrent()
    {
        var manager = CreateManager();
        var exports = manager.GetExports<IDuplicateService>().ToList();

        Assert.Single(exports);
    }

    [Fact]
    public void LocationLookup()
    {
        var manager = CreateManager();
        var export = manager.GetExports<ITestService>().First();
        var byLocation = manager.GetExport<ITestService>(export.Location);

        Assert.Same(export, byLocation);
    }

    [Fact]
    public void ManyContractNames()
    {
        var manager = CreateManager();
        var exports = manager.GetManyExports<ITestService>([null, "Named", "Named"]).ToList();

        Assert.Contains(exports, x => x.ValueType == typeof(AlphaService));
        Assert.Single(exports, x => x.ValueType == typeof(NamedService));
    }

    [Fact]
    public void AssemblyUnload()
    {
        var manager = CreateManager();
        manager.UnloadAssembly(typeof(ExportComponentManagerTests).Assembly);

        Assert.Empty(manager.GetExports<ITestService>());
        Assert.Empty(manager.GetLoadedAssemblies());
    }

    [Fact]
    public void ManualRegistration()
    {
        var manager = new ExportComponentManager();
        var component = new ExportComponent<ITestService>(null, typeof(ITestService), typeof(AlphaService));

        manager.Register(component);

        Assert.Same(component, manager.GetExport<ITestService>(component.Location));
        Assert.Contains(component, manager.GetExports<ITestService>());
    }

    [Fact]
    public void DuplicateRegistration()
    {
        var manager = new ExportComponentManager();
        manager.Register(new ExportComponent<ITestService>(null, typeof(ITestService), typeof(AlphaService)));

        Assert.Throws<InvalidOperationException>(
            () => manager.Register(new ExportComponent<ITestService>(null, typeof(ITestService), typeof(AlphaService))));
    }

    [Fact]
    public void RegistrationEvent()
    {
        var manager = new ExportComponentManager();
        var changed = new List<Type>();
        manager.ExportCollectionChanged += (_, e) => changed.AddRange(e.ChangedContractsTypes);

        manager.Register(new ExportComponent<ITestService>(null, typeof(ITestService), typeof(AlphaService)));

        Assert.Equal([typeof(ITestService)], changed);
    }

    [Fact]
    public void UnregistrationEvent()
    {
        var manager = new ExportComponentManager();
        var component = new ExportComponent<ITestService>(null, typeof(ITestService), typeof(AlphaService));
        manager.Register(component);

        var changed = new List<Type>();
        manager.ExportCollectionChanged += (_, e) => changed.AddRange(e.ChangedContractsTypes);

        Assert.True(manager.Unregister(component));
        Assert.Equal([typeof(ITestService)], changed);
        Assert.Empty(manager.GetExports<ITestService>());
    }

    [Fact]
    public void LocationUnregistration()
    {
        var manager = new ExportComponentManager();
        var component = new ExportComponent<ITestService>(null, typeof(ITestService), typeof(AlphaService));
        manager.Register(component);

        Assert.True(manager.Unregister(component.Location));
        Assert.Null(manager.GetExport<ITestService>(component.Location));
        Assert.Empty(manager.GetExports<ITestService>());
    }

    [Fact]
    public void ContractTypeNotification()
    {
        var manager = new ExportComponentManager();
        var raised = 0;
        var handler = new EventHandler((_, _) => raised++);

        manager.AddNotify(typeof(ITestService), handler);
        manager.Register(new ExportComponent<ITestService>(null, typeof(ITestService), typeof(AlphaService)));
        Assert.Equal(1, raised);

        manager.RemoveNotify(typeof(ITestService), handler);
        manager.Register(new ExportComponent<ITestService>("Second", typeof(ITestService), typeof(BetaService)));
        Assert.Equal(1, raised);
    }

    private static ExportComponentManager CreateManager()
    {
        var manager = new ExportComponentManager();
        manager.LoadAssembly(typeof(ExportComponentManagerTests).Assembly);
        return manager;
    }
}
