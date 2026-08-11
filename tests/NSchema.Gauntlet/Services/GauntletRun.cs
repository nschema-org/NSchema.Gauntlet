using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Cli;
using NSchema.Gauntlet.Services.Corpus;
using NSchema.Gauntlet.Services.Coverage;
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
        Observer = new PlanObserver(Cli, Coverage);
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
    /// Gets which actions the run has exercised, accumulated across every case.
    /// </summary>
    public ActionLedger Coverage { get; } = new();

    /// <summary>
    /// Gets the planner that records what it plans.
    /// </summary>
    public PlanObserver Observer { get; }

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
        var builder = new PluginBuilder();

        foreach (var (_, plugin) in _settings.Engines.Plugins)
        {
            if (plugin.Project is { Length: > 0 } project)
            {
                plugin.BuiltAt(await builder.Build(project, CancellationToken.None));
            }
        }
    }

    // Written rather than asserted. Coverage is a measurement of the suite, not a claim about the engine: a
    // filtered run legitimately exercises less, so failing on a number would fail for running fewer tests. The
    // report is an artifact to read, and it says how much of the suite it saw.
    private async ValueTask WriteCoverage()
    {
        if (Coverage.IsEmpty)
        {
            return;
        }

        try
        {
            var surface = ActionSurface.Read(await Cli.ResolveDirectory(CancellationToken.None));
            var report = Coverage.Describe(surface, [.. Engines.Names]);

            var directory = Path.Combine(_settings.Root, "artifacts");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "coverage.md"), report);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // A run that finished has already said what it had to say; failing its teardown over the bookkeeping
            // would replace a result with a report about not writing a report.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await WriteCoverage();
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
