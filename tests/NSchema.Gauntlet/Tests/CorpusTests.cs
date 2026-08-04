using NSchema.Gauntlet.Runner;
using NSchema.Gauntlet.Services;

namespace NSchema.Gauntlet.Tests;

public sealed class CorpusTests(GauntletRun run)
{
    [Theory]
    [MemberData(nameof(GauntletMatrix.CorpusAndEngines), MemberType = typeof(GauntletMatrix))]
    public async Task Corpus(string name, string engineName)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var corpus = run.Corpus.Get(name);
        var engine = run.Engines.Get(engineName);

        // Act
        var outcome = await new CorpusRunner(run.Cli, engine, run.PackageSources).Run(name, corpus.Ddl[engineName], ct);

        // Assert
        outcome.SetupFailure.ShouldBeNull(outcome.SetupFailure?.Describe());

        await Verify(outcome.Report()).UseTextForParameters($"{name}.{engineName}");

        outcome.Canonical.ShouldBeTrue($"the imported project was not canonical:{Environment.NewLine}{outcome.Format?.Describe()}");
        outcome.RoundTrips.ShouldBeTrue($"NSchema found differences against its own description of the database:{Environment.NewLine}{outcome.Verification?.Describe()}");
        outcome.Rebuilds.ShouldBeTrue($"the schema NSchema rendered was not the schema it described:{Environment.NewLine}{outcome.RebuildVerification?.Describe()}");
        outcome.Faithful.ShouldNotBe(false,
            $"the engine's own account of the rebuild differs from the source:{Environment.NewLine}{string.Join(Environment.NewLine, outcome.EngineTestimony ?? [])}");
        outcome.TearsDown.ShouldBeTrue($"the schema would not come apart again:{Environment.NewLine}{outcome.TeardownVerification?.Describe()}");
    }
}
