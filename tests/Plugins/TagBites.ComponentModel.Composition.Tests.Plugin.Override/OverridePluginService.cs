using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using TagBites.ComponentModel.Composition;

[assembly: AssemblyExportSettings(DuplicateUriHandling = ExportDuplicateUriHandling.OverrideExisting)]

namespace TagBites.ComponentModel.Composition.Tests.Plugin.Override;

[Export(typeof(IPluginService))]
[Guid(PluginService.SharedGuid)]
public class OverridePluginService : IPluginService
{
    public string Name => "Override";
}
