using System.ComponentModel.Composition;
using System.Runtime.InteropServices;

namespace TagBites.ComponentModel.Composition.Tests.Plugin;

public interface IPluginService
{
    string Name { get; }
}

/// <summary>
/// Export of the plugin loaded first. The <see cref="GuidAttribute"/> is what makes the export URI
/// independent from the type name, so an implementation in another assembly can collide with it.
/// </summary>
[Export(typeof(IPluginService))]
[Guid(SharedGuid)]
public class PluginService : IPluginService
{
    public const string SharedGuid = "C7F0B0A1-4E2D-4F3B-9A1C-2D5E6F7A8B90";

    public string Name => "Plugin";
}
