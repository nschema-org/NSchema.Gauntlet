namespace NSchema.Gauntlet.Services.Engines;

/// <summary>
/// A provider package, as a project declares it.
/// </summary>
public sealed class PluginSettings
{
    /// <summary>
    /// The name of the package to install.
    /// </summary>
    public required string Package { get; init; }

    /// <summary>
    /// The version of the package to install.
    /// </summary>
    public required string Version { get; init; }
}
