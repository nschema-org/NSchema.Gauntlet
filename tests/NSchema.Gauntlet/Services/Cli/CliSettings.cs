namespace NSchema.Gauntlet.Services.Cli;

/// <summary>
/// The CLI a run is pinned to.
/// </summary>
public sealed class CliSettings
{
    /// <summary>
    /// The tool package id.
    /// </summary>
    public required string Package { get; init; }

    /// <summary>
    /// The exact version the run installs.
    /// </summary>
    public required string Version { get; init; }
}
