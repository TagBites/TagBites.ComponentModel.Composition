using System.ComponentModel;

namespace TagBites.ComponentModel.Composition;

[PublicAPI]
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Assembly)]
public class AssemblyExportSettingsAttribute : Attribute
{
    public ExportDuplicateUriHandling DuplicateUriHandling { get; set; }
}
