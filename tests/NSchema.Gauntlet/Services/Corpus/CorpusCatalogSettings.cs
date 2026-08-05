namespace NSchema.Gauntlet.Services.Corpus;

/// <summary>
/// Where the corpus cases are read from.
/// </summary>
public sealed class CorpusCatalogSettings
{
    /// <summary>
    /// The directory, relative to the root, where corpus cases are held.
    /// </summary>
    public string Directory { get; init; } = "corpus";

    /// <summary>
    /// The name of the manifest file from which to load settings.
    /// </summary>
    public string Manifest { get; init; } = "manifest.json";
}
