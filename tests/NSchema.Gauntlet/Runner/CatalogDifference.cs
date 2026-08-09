namespace NSchema.Gauntlet.Runner;

/// <summary>
/// One symptom: an attribute of one catalog relation disagreeing the same way, across however many objects
/// it fires on.
/// </summary>
public sealed record CatalogDifference(
    string Catalog,
    string Attribute,
    CatalogChange Change,
    IReadOnlyList<CatalogOccurrence> Occurrences
);
