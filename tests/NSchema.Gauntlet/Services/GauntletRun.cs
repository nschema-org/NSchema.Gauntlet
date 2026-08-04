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
        Engines = new EngineFleet(_settings.Engines);
        Cli = new CliInstallation(_settings.Cli, _settings.PackageSources);
    }

    /// <summary>
    /// Extra package sources the run's projects and tool install draw from.
    /// </summary>
    public IReadOnlyList<string> PackageSources => _settings.PackageSources;

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
    public ValueTask InitializeAsync()
    {
        // A version pinned from a local source is mutable, so no cache may vouch for it.
        PackageCache.Clear(_settings, Cli.Directory);
        return Cli.Install(CancellationToken.None);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Engines.DisposeAsync();
}
