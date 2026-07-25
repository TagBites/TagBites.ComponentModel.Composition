using System.Reflection;
using TagBites.ComponentModel.Composition.Tests.Plugin;
using TagBites.ComponentModel.Composition.Tests.Plugin.Override;
using TagBites.ComponentModel.Composition.Tests.Plugin.Remove;
using Xunit;

namespace TagBites.ComponentModel.Composition.Tests;

public class DuplicateUriTests
{
    private static readonly Assembly s_pluginAssembly = typeof(PluginService).Assembly;
    private static readonly Assembly s_overrideAssembly = typeof(OverridePluginService).Assembly;
    private static readonly Assembly s_removeAssembly = typeof(RemovePluginService).Assembly;


    [Fact]
    public void CollidingUri()
    {
        var plugin = ExportComponentDefinition.GetDefaultUri(typeof(IPluginService), null, typeof(PluginService));
        var over = ExportComponentDefinition.GetDefaultUri(typeof(IPluginService), null, typeof(OverridePluginService));
        var remove = ExportComponentDefinition.GetDefaultUri(typeof(IPluginService), null, typeof(RemovePluginService));

        Assert.Equal(plugin, over);
        Assert.Equal(plugin, remove);
    }

    [Fact]
    public void SkipCurrentKeepsExistingExport()
    {
        var manager = CreateManager(s_removeAssembly);

        // Plugin assembly declares no settings, so the default applies
        manager.LoadAssembly(s_pluginAssembly);

        AssertSingleExport<RemovePluginService>(manager);
    }

    [Fact]
    public void OverrideExistingWinsUriLookup()
    {
        var manager = CreateManager(s_pluginAssembly);
        manager.LoadAssembly(s_overrideAssembly);

        Assert.IsType<OverridePluginService>(manager.GetExportInstance<IPluginService>(GetLocation()));
    }

    [Fact]
    public void OverrideExistingRestoresOnUnload()
    {
        var manager = CreateManager(s_pluginAssembly);
        manager.LoadAssembly(s_overrideAssembly);
        manager.UnloadAssembly(s_overrideAssembly);

        Assert.IsType<PluginService>(manager.GetExportInstance<IPluginService>(GetLocation()));
        AssertSingleExport<PluginService>(manager);
    }

    [Fact]
    public void RemoveExistingDropsExistingExport()
    {
        var manager = CreateManager(s_pluginAssembly);
        manager.LoadAssembly(s_removeAssembly);

        AssertSingleExport<RemovePluginService>(manager);
    }

    [Fact]
    public void RemoveExistingRestoresOnUnload()
    {
        var manager = CreateManager(s_pluginAssembly);
        manager.LoadAssembly(s_removeAssembly);
        manager.UnloadAssembly(s_removeAssembly);

        AssertSingleExport<PluginService>(manager);
    }

    [Fact]
    public void RemoveExistingSurvivesReload()
    {
        var manager = CreateManager(s_pluginAssembly);

        for (var i = 0; i < 3; i++)
        {
            manager.LoadAssembly(s_removeAssembly);
            AssertSingleExport<RemovePluginService>(manager);

            manager.UnloadAssembly(s_removeAssembly);
            AssertSingleExport<PluginService>(manager);
        }
    }

    [Fact]
    public void RemoveExistingKeepsNoRestoreRecord()
    {
        var manager = CreateManager(s_pluginAssembly);
        manager.LoadAssembly(s_removeAssembly);
        Assert.Equal(1, GetRestoreRecordCount(manager));

        manager.UnloadAssembly(s_removeAssembly);
        Assert.Equal(0, GetRestoreRecordCount(manager));
    }

    [Fact]
    public void RemoveExistingKeepsExportsOfUnloadedOrigin()
    {
        var manager = CreateManager(s_pluginAssembly);
        manager.LoadAssembly(s_removeAssembly);
        manager.UnloadAssembly(s_pluginAssembly);
        manager.UnloadAssembly(s_removeAssembly);

        Assert.Empty(manager.GetExports<IPluginService>());
    }

    private static ExportComponentManager CreateManager(Assembly assembly)
    {
        var manager = new ExportComponentManager();
        manager.LoadAssembly(assembly);
        return manager;
    }
    private static Uri GetLocation()
    {
        return ExportComponentDefinition.GetDefaultUri(typeof(IPluginService), null, typeof(PluginService));
    }
    private static void AssertSingleExport<T>(ExportComponentManager manager)
    {
        var exports = manager.GetExports<IPluginService>().ToList();

        Assert.Single(exports);
        Assert.Equal(typeof(T), exports[0].ValueType);
    }
    /// <summary>
    /// Reads the private list of exports kept for restoring. The list is invisible in the public API,
    /// so this is the only way to catch records that outlive the unload that consumes them.
    /// </summary>
    private static int GetRestoreRecordCount(ExportComponentManager manager)
    {
        var field = typeof(ExportComponentManager).GetField("_removedExports", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        return ((ICollection)field.GetValue(manager)!).Count;
    }
}
