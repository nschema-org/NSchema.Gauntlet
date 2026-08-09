using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Cli;
using NSchema.Gauntlet.Services.Corpus;
using NSchema.Gauntlet.Services.Engines;
using NSchema.Gauntlet.Services.Scenarios;

namespace NSchema.Gauntlet.Services;

/// <summary>
/// One run: its settings, its cases, and the engines it has brought up.
/// </summary>
public sealed class GauntletRun : IAsyncLifetime
{
    private readonly GauntletSettings _settings = GauntletSettings.Load();

    /// <summary>
    /// Builds a run from the settings on disk.
    /// </summary>
    public GauntletRun()
    {
        Scenarios = new ScenarioCatalog(_settings.Root, _settings.Scenarios);
        Corpus = new CorpusCatalog(_settings.Root, _settings.Corpus);
        Engines = new EngineFleet(_settings.Engines, _settings.TempDirectory);
        Cli = new NSchemaClient(_settings.Cli, _settings.NuGet, _settings.TempDirectory);
    }

    /// <summary>
    /// Gets the scenarios active in the run.
    /// </summary>
    public ScenarioCatalog Scenarios { get; }

    /// <summary>
    /// Gets the corpus cases active in the run.
    /// </summary>
    public CorpusCatalog Corpus { get; }

    /// <summary>
    /// Gets the engines active in the run.
    /// </summary>
    public EngineFleet Engines { get; }

    /// <summary>
    /// Gets the CLI the run drives.
    /// </summary>
    public NSchemaClient Cli { get; }

    /// <summary>
    /// Creates and configures a new project.
    /// </summary>
    public Project Project(Database database)
    {
        var directory = Path.Combine(_settings.TempDirectory, "projects", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        _settings.NuGet.WriteConfig(directory);
        var project = new Project(directory);
        project.ConnectTo(database);
        return project;
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var publisher = new PluginPublisher(_settings.TempDirectory);

        foreach (var (_, plugin) in _settings.Engines.Plugins)
        {
            if (plugin.Project is { Length: > 0 } project)
            {
                await publisher.Publish(project, plugin.Package, plugin.Version, CancellationToken.None);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Engines.DisposeAsync();
        try
        {
            Directory.Delete(_settings.TempDirectory, recursive: true);
        }
        catch
        {
            // Ignore.
        }
    }
}
