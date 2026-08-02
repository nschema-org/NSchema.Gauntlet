namespace NSchema.Gauntlet.Services.Scenarios;

public record ScenarioCatalogSettings
{
    /// <summary>
    /// The directory, relative to the root, where scenarios are held.
    /// </summary>
    public string Directory { get; set; } = "scenarios";

    /// <summary>
    /// The name of the manifest file from which to load settings.
    /// </summary>
    public string Manifest { get; init; } = "manifest.json";
}
