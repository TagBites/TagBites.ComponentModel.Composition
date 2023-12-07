# TBS.ComponentModel.Composition

[![Nuget](https://img.shields.io/nuget/v/TagBites.ComponentModel.Composition.svg)](https://www.nuget.org/packages/TagBites.ComponentModel.Composition/)
[![License](http://img.shields.io/github/license/TagBites/TagBites.ComponentModel.Composition)](https://github.com/TagBites/TagBites.ComponentModel.Composition/blob/master/LICENSE)

A .NET export container and manager designed to simplify access to exported types. Well suited for plugin integration. Supports caching to speed up application startup, eliminating the necessity to load and analyze all assemblies.

## Example

```csharp
internal static class Program
{
    public static ExportComponentManager ComponentManager { get; set; }


    static void Main()
    {
        // Load
        ComponentManager = new ExportComponentManager();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            ComponentManager.LoadAssembly(assembly);

        // Test
        foreach (var testInstance in ComponentManager.GetExportInstances<ITest>())
            Console.WriteLine(testInstance.GetType().Name);

        // Output:
        // Test1
        // Test2
    }
}

public interface ITest
{ }

[Export(typeof(ITest))]
internal class Test1 : ITest
{ }

[Export(typeof(ITest))]
internal class Test2 : ITest
{ }
```
