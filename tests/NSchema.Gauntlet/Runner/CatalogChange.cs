namespace NSchema.Gauntlet.Runner;

/// <summary>
/// Which way a catalog fact disagrees.
/// </summary>
public enum CatalogChange
{
    /// <summary>
    /// Both databases have the fact, and they do not agree on its value.
    /// </summary>
    Changed,

    /// <summary>
    /// The source has the fact and the rebuild does not.
    /// </summary>
    MissingFromRebuild,

    /// <summary>
    /// The rebuild has the fact and the source does not.
    /// </summary>
    OnlyInRebuild,
}
