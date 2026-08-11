namespace NSchema.Gauntlet.Runner;

/// <summary>
/// One object's disagreement about one attribute.
/// </summary>
public sealed record CatalogOccurrence(string Identity, string Source, string Rebuild);
