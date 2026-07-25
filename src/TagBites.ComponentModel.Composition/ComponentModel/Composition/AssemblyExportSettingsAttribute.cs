namespace TagBites.ComponentModel.Composition;

/// <summary>
/// Sets how the container handles exports of this assembly whose URI is already registered.
/// </summary>
/// <example>
/// <code>[assembly: AssemblyExportSettings(DuplicateUriHandling = ExportDuplicateUriHandling.OverrideExisting)]</code>
/// </example>
[PublicAPI]
[AttributeUsage(AttributeTargets.Assembly)]
public class AssemblyExportSettingsAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the behavior for an export whose URI is already registered.
    /// Default: <see cref="ExportDuplicateUriHandling.SkipCurrent"/>.
    /// </summary>
    public ExportDuplicateUriHandling DuplicateUriHandling { get; set; }
}
