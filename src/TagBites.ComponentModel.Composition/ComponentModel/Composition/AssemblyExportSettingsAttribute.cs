namespace TagBites.ComponentModel.Composition;

[PublicAPI]
[AttributeUsage(AttributeTargets.Assembly)]
public class AssemblyExportSettingsAttribute : Attribute
{
    public ExportDuplicateUriHandling DuplicateUriHandling { get; set; }
}
