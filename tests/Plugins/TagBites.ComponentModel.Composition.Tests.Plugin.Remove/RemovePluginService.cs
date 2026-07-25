using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using TagBites.ComponentModel.Composition;

[assembly: AssemblyExportSettings(DuplicateUriHandling = ExportDuplicateUriHandling.RemoveExisting)]

namespace TagBites.ComponentModel.Composition.Tests.Plugin.Remove;

[Export(typeof(IPluginService))]
[Guid(PluginService.SharedGuid)]
public class RemovePluginService : IPluginService
{
    public string Name => "Remove";
}
