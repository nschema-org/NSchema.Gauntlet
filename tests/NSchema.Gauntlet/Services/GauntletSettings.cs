using Microsoft.Extensions.Configuration;
using NSchema.Gauntlet.Services.Cli;
using NSchema.Gauntlet.Services.Corpus;
using NSchema.Gauntlet.Services.Engines;
using NSchema.Gauntlet.Services.Scenarios;

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
    public required ScenarioCatalogSettings Scenarios { get; init; }

    /// <summary>
    /// Where the corpus cases are.
    /// </summary>
    public required CorpusCatalogSettings Corpus { get; init; }

    /// <summary>
    /// Gets the settings for the different database engines.
    /// </summary>
    public required EngineSettings Engines { get; init; }

    /// <summary>
    /// Extra NuGet package sources for the run, beyond the defaults.
    /// </summary>
    public IReadOnlyList<string> PackageSources { get; init; } = [];

    /// <summary>
    /// Gets the CLI the run is pinned to.
    /// </summary>
    public required CliSettings Cli { get; init; }

    /// <summary>
    /// Reads the settings for a run.
    /// </summary>
    public static GauntletSettings Load() => new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile("appsettings.local.json", optional: true)
        .Build()
        .Get<GauntletSettings>() ?? throw new InvalidOperationException("appsettings.json is empty.");

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
        throw new InvalidOperationException($"Could not locate {rootMarker} above {AppContext.BaseDirectory}.");
    }
}
