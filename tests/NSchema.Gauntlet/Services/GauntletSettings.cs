using Microsoft.Extensions.Configuration;

namespace NSchema.Gauntlet.Services;

/// <summary>
/// What a run is pinned to: every image tag and package version, in one place.
/// </summary>
public sealed class GauntletSettings
{
    /// <summary>
    /// Gets the root directory of the repository.
    /// </summary>
    public string Root { get; } = RepositoryRoot();

    /// <summary>
    /// Where the case directories are.
    /// </summary>
    public ScenarioCatalogSettings Scenarios { get; set; } = new();

    /// <summary>
    /// Gets the settings for the different database engines.
    /// </summary>
    public Dictionary<string, EngineSettings> Engines { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the settings for a run.
    /// </summary>
    public static GauntletSettings Load() => new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .Build()
        .Get<GauntletSettings>() ?? throw new InvalidOperationException("appsettings.json is empty.");

    /// <summary>
    /// The settings for one engine.
    /// </summary>
    public EngineSettings Engine(string name) => Engines.TryGetValue(name, out var settings)
        ? settings
        : throw new InvalidOperationException($"No engine is configured as '{name}' in appsettings.json.");

    // Finding the repository is not configuration: an absolute path in a settings file is one nobody
    // else's checkout can use. Lazy, so configuring a root skips the walk entirely.
    private static string RepositoryRoot()
    {
        const string rootMarker = ".git";
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, rootMarker)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate {rootMarker} above {AppContext.BaseDirectory}. Set paths:root to say where the cases are.");
    }
}
