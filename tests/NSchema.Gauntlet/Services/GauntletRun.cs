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
    /// <summary>
    /// Builds a run from the settings on disk.
    /// </summary>
    public GauntletRun()
    {
        var settings = GauntletSettings.Load();
        Scenarios = new ScenarioCatalog(settings.Root, settings.Scenarios);
        Corpus = new CorpusCatalog(settings.Root, settings.Corpus);
        Engines = new EngineFleet(settings.Engines);
        Cli = new CliInstallation(settings.Cli);
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
    public CliInstallation Cli { get; }

    /// <inheritdoc />
    public ValueTask InitializeAsync() => Cli.Install(CancellationToken.None);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Engines.DisposeAsync();
}
