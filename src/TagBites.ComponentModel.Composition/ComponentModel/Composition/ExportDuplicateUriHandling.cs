namespace TagBites.ComponentModel.Composition;

public enum ExportDuplicateUriHandling
{
    /// <summary>
    /// Skip current export.
    /// </summary>
    SkipCurrent,
    /// <summary>
    /// Override existing export.
    /// </summary>
    OverrideExisting,
    /// <summary>
    /// Remove existing export.
    /// </summary>
    RemoveExisting
}
