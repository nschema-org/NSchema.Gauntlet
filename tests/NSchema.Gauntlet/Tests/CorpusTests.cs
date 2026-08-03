using NSchema.Gauntlet.Model;
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
        await using var database = await engine.CreateDatabase($"{name}_{engineName}", ct);
        using var project = GauntletProject.Create(engine, database);

        // Act
        var outcome = await new CorpusRunner(project).Run(database, corpus.Ddl[engineName], ct);

        // Assert
        outcome.SetupFailure.ShouldBeNull(outcome.SetupFailure?.Describe());

        await Verify(outcome.Report()).UseTextForParameters($"{name}.{engineName}");

        outcome.Canonical.ShouldBeTrue($"the imported project was not canonical:{Environment.NewLine}{outcome.Format?.Describe()}");
        outcome.RoundTrips.ShouldBeTrue($"NSchema found differences against its own description of the database:{Environment.NewLine}{outcome.Verification?.Describe()}");
    }
}
