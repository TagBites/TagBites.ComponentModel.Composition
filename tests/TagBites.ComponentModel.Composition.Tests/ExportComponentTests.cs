using TagBites.ComponentModel.Composition.Tests.Models;
using Xunit;

namespace TagBites.ComponentModel.Composition.Tests;

public class ExportComponentTests
{
    [Fact]
    public void SharedInstance()
    {
        var component = new ExportComponent<ITestService>(null, typeof(ITestService), typeof(AlphaService));

        Assert.Same(component.Instance, component.Instance);
    }

    [Fact]
    public void NewInstancePerCreateCall()
    {
        var component = new ExportComponent<ITestService>(null, typeof(ITestService), typeof(AlphaService));

        Assert.NotSame(component.CreateInstance(), component.CreateInstance());
    }

    [Fact]
    public void InstanceThreadSafety()
    {
        var component = new ExportComponent<ITestService>(null, typeof(ITestService), typeof(AlphaService));
        var instances = new object[64];

        Parallel.For(0, instances.Length, i => instances[i] = component.Instance);

        Assert.Single(instances.Distinct());
    }

    [Fact]
    public void InstanceProvider()
    {
        var instance = new AlphaService();
        var component = new ExportComponent<ITestService>(null, typeof(ITestService), typeof(AlphaService), null, () => instance, null);

        Assert.Same(instance, component.CreateInstance());
        Assert.Same(instance, component.Instance);
    }

    [Fact]
    public void DefaultLocation()
    {
        var component = new ExportComponent<ITestService>(null, typeof(ITestService), typeof(AlphaService));
        var expected = ExportComponentDefinition.GetDefaultUri(typeof(ITestService), null, typeof(AlphaService));

        Assert.Equal(expected, component.Location);
    }
}
