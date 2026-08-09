namespace NSchema.Gauntlet.Services.Cli;

/// <summary>
/// The CLI a run is pinned to.
/// </summary>
public sealed class CliSettings
{
    /// <summary>
    /// The tool package id. Ignored when <see cref="Path"/> is set.
    /// </summary>
    public string? Package { get; init; }

    /// <summary>
    /// The exact version the run installs. Ignored when <see cref="Path"/> is set.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// The path to an NSchema executable to run instead of installing one.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// How this CLI should be described in a report.
    /// </summary>
    public string Describe() => Path is { Length: > 0 } path ? $"{path} (unreleased build)" : $"{Package} {Version}";

    /// <summary>
    /// Fails when neither a package nor a path is configured, or when a configured path does not exist.
    /// </summary>
    public void Validate()
    {
        if (Path is { Length: > 0 } path)
        {
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"The configured CLI path '{path}' does not exist.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(Package) || string.IsNullOrWhiteSpace(Version))
        {
            throw new InvalidOperationException("The CLI declares neither a package and version nor a path.");
        }
    }
}
