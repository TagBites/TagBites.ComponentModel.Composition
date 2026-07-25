using System.Text.Json;
using TagBites.ComponentModel.Composition.Tests.Models;
using Xunit;

namespace TagBites.ComponentModel.Composition.Tests;

public class AssemblyCacheTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), "TagBites.ComponentModel.Composition.Tests", Guid.NewGuid().ToString("N"));


    [Fact]
    public void CacheRoundTrip()
    {
        var assembly = typeof(AssemblyCacheTests).Assembly;

        var first = CreateManager(out var firstCounters);
        first.LoadAssembly(assembly);
        first.PrepareCache();

        Assert.Equal(1, firstCounters.Writes);
        Assert.Single(Directory.GetFiles(_cacheDirectory, "*.json"));

        var second = CreateManager(out var secondCounters);
        second.LoadAssembly(assembly);

        Assert.Equal(1, secondCounters.Reads);
        Assert.NotEmpty(second.GetExports<ITestService>());
        Assert.IsType<AlphaService>(second.GetExportInstances<ITestService>().First(x => x is AlphaService));
    }

    [Fact]
    public void StaleFileCleanup()
    {
        var assembly = typeof(AssemblyCacheTests).Assembly;
        var name = assembly.GetName();

        Directory.CreateDirectory(_cacheDirectory);
        var staleFile = Path.Combine(_cacheDirectory, $"{name.Name}-{name.Version}-{Guid.NewGuid():N}.json");
        File.WriteAllText(staleFile, "{}");

        var manager = CreateManager(out _);
        manager.LoadAssembly(assembly);
        manager.PrepareCache();

        Assert.False(File.Exists(staleFile));
        Assert.Single(Directory.GetFiles(_cacheDirectory, "*.json"));
    }

    private ExportComponentManager CreateManager(out CacheCounters counters)
    {
        var manager = new ExportComponentManager();
        var localCounters = new CacheCounters();

        manager.UseCache(
            _cacheDirectory,
            (file, type) =>
            {
                localCounters.Reads++;
                return JsonSerializer.Deserialize(File.ReadAllText(file), type);
            },
            (file, model) =>
            {
                localCounters.Writes++;
                File.WriteAllText(file, JsonSerializer.Serialize(model));
            });

        counters = localCounters;
        return manager;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_cacheDirectory, true);
        }
        catch { /* ignored */ }
    }

    private sealed class CacheCounters
    {
        public int Reads { get; set; }
        public int Writes { get; set; }
    }
}
