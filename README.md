# TagBites.ComponentModel.Composition

[![Nuget](https://img.shields.io/nuget/v/TagBites.ComponentModel.Composition.svg)](https://www.nuget.org/packages/TagBites.ComponentModel.Composition/)
[![Build](https://img.shields.io/github/actions/workflow/status/TagBites/TagBites.ComponentModel.Composition/build-and-test.yml?branch=master)](https://github.com/TagBites/TagBites.ComponentModel.Composition/actions)
[![License](https://img.shields.io/github/license/TagBites/TagBites.ComponentModel.Composition)](https://github.com/TagBites/TagBites.ComponentModel.Composition/blob/master/LICENSE)

A lightweight export container for plugin-based .NET applications. The container discovers types marked with the MEF `[Export]` attribute and gives access to them by contract type, contract name or URI.

## Install

```bash
dotnet add package TagBites.ComponentModel.Composition
```

Targets `netstandard2.0`. Only dependency is `System.ComponentModel.Composition`, used for the standard `[Export]` attribute.

## Why TagBites.ComponentModel.Composition?

MEF (`CompositionContainer`) composes full object graphs with imports, exports and lifetime policies. This library solves a smaller problem: it keeps a registry of exported types and creates instances on demand. In exchange it offers:
- **Explicit lifecycle.** Assemblies are loaded and unloaded on demand. Unloading an assembly removes its exports and restores the ones it replaced.
- **Stable identity.** Every export has a URI. The URI addresses one implementation and allows a plugin to replace an implementation from another assembly.
- **Change notifications.** Events report changed contract types when exports are loaded, unloaded, registered or unregistered.
- **Lazy instances.** Each export provides a shared instance (created on first use, held by a weak reference) or a new instance per call.

Typical use cases: plugin systems, modular desktop applications, applications with many optional assemblies and a slow cold start.

## Usage

### Discover exports

```csharp
public interface IImporter
{
    string Name { get; }
}

[Export(typeof(IImporter))]
public class CsvImporter : IImporter
{
    public string Name => "CSV";
}

[Export(typeof(IImporter))]
public class XmlImporter : IImporter
{
    public string Name => "XML";
}
```

```csharp
var manager = new ExportComponentManager();
manager.LoadAssembly(typeof(CsvImporter).Assembly);

foreach (var importer in manager.GetExportInstances<IImporter>())
    Console.WriteLine(importer.Name);

// CSV
// XML
```

### Shared and new instances

`GetExportInstance` returns a shared instance. The instance is created on first use and held by a weak reference, so the garbage collector may reclaim it; the next access creates a new one. `CreateExportInstance` returns a new instance on every call.

```csharp
var shared = manager.GetExportInstances<IImporter>().First();
var again = manager.GetExportInstances<IImporter>().First();
// ReferenceEquals(shared, again) == true

var fresh = manager.CreateExportInstances<IImporter>().First();
// ReferenceEquals(shared, fresh) == false
```

### Contract names

A contract name separates exports of the same contract type into groups.

```csharp
[Export("analytics", typeof(IImporter))]
public class AnalyticsImporter : IImporter
{
    public string Name => "Analytics";
}
```

```csharp
manager.GetExportInstances<IImporter>("analytics"); // AnalyticsImporter
manager.GetExportInstances<IImporter>();            // exports without a contract name
manager.GetManyExports<IImporter>([null, "analytics"]); // both groups
```

### Export URI

Every export has a `Location` URI built from the contract type, the contract name and the implementation type: `export:{contract}/{name}/{implementation}`. A type is identified by its full name with assembly name, or by its `[Guid]` attribute when present.

```csharp
var export = manager.GetExports<IImporter>().First();
Console.WriteLine(export.Location);
// export:MyApp.IImporter,MyApp/MyApp.CsvImporter,MyApp

var instance = manager.GetExportInstance<IImporter>(export.Location);
// shared CsvImporter instance
```

A `[Guid]` attribute makes the identity independent from the type name and assembly. Two implementations in different assemblies with the same `[Guid]` share the URI, which allows one to replace the other.

### Duplicate URIs

When a loaded assembly contains an export whose URI is already registered, the `AssemblyExportSettings` assembly attribute decides the outcome:

```csharp
[assembly: AssemblyExportSettings(DuplicateUriHandling = ExportDuplicateUriHandling.OverrideExisting)]
```

| Mode | Behavior |
| --- | --- |
| `SkipCurrent` | The new export is ignored. Default. |
| `OverrideExisting` | The new export replaces the existing one for URI lookups. Both remain listed for the contract. |
| `RemoveExisting` | The existing export is removed. Unloading the new assembly restores it. |

### Manual registration

Components can be registered without the `[Export]` attribute. An instance provider supports types without a default constructor.

```csharp
var component = new ExportComponent<IImporter>(null, typeof(IImporter), typeof(CsvImporter));
manager.Register(component);
manager.Unregister(component);
```

```csharp
var component = new ExportComponent<IImporter>(
    null, typeof(IImporter), typeof(DatabaseImporter),
    null, () => new DatabaseImporter(connectionString), null);
manager.Register(component);
```

### Change notifications

`ExportCollectionChanged` reports every change with the list of affected contract types. `AddNotify` subscribes to one contract type.

```csharp
manager.ExportCollectionChanged += (s, e) => Refresh(e.ChangedContractsTypes);

manager.AddNotify(typeof(IImporter), OnImportersChanged);
manager.RemoveNotify(typeof(IImporter), OnImportersChanged);
```

### Unloading

`UnloadAssembly` removes all exports of an assembly and restores exports it replaced.

```csharp
manager.UnloadAssembly(pluginAssembly);
```

### Startup cache

`LoadAssembly` reflects over all types of an assembly. With many assemblies this cost dominates application startup. `UseCache` stores the scan result of each assembly in a JSON file, so later starts read one small file per assembly instead. The file name includes the assembly module version id, so a rebuilt assembly bypasses the stale file automatically. An unreadable or corrupted file falls back to reflection.

The serializer is provided by the caller, so the library has no serializer dependency:

```csharp
var manager = new ExportComponentManager();
manager.UseCache(
    Path.Combine(AppContext.BaseDirectory, "export-cache"),
    (file, type) => JsonSerializer.Deserialize(File.ReadAllText(file), type),
    (file, model) => File.WriteAllText(file, JsonSerializer.Serialize(model)));

foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    manager.LoadAssembly(assembly);

// After startup, e.g. from a background task
manager.PrepareCache();
```

`PrepareCache` writes files for assemblies loaded without a cache hit and removes stale files of previous builds. The cache stores contract type names; `UseCustomTypeResolver` replaces the default `Type.GetType` resolution when needed.

## Links

- [Changelog](https://github.com/TagBites/TagBites.ComponentModel.Composition/blob/master/CHANGELOG.md)
- [Security policy](https://github.com/TagBites/TagBites.ComponentModel.Composition/blob/master/SECURITY.md)
- [License (MIT)](https://github.com/TagBites/TagBites.ComponentModel.Composition/blob/master/LICENSE)
