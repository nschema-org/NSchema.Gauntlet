using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Runner;
using NSchema.Gauntlet.Services;

namespace NSchema.Gauntlet.Tests;

public sealed class CorpusTests(GauntletRun run)
{
    [Theory]
    [MemberData(nameof(GauntletMatrix.CorpusAndEngines), MemberType = typeof(GauntletMatrix))]
    public async Task Corpus(CorpusName corpusName, EngineName engineName)
    {
        // Arrange
        var runner = new CorpusRunner(run.Cli, run.Project);
        var corpus = run.Corpus.Get(corpusName);
        var engine = run.Engines.Get(engineName);

        // Act
        var result = await runner.Run(corpus, engine, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse(result.Describe());
        var report = result.Value;

        corpus.Expectations.TryGetValue(engineName, out var expectation).ShouldBeTrue(
            $"Corpus '{corpusName}' sets no expectation for '{engineName}'. " +
            $"Declare the expectation in the corpus manifest.");

        switch (expectation.Outcome == CorpusOutcome.Succeeded)
        {
            case false when expectation.Because is null:
                throw new Exception($"'{corpusName}' expects '{engineName}' to fail but declares no reason; say which capability gap this documents.");
            case true when expectation.Because is not null:
                throw new Exception($"'{corpusName}' expects '{engineName}' to succeed; remove the leftover reason from the manifest.");
        }

        var evidence = report.Outcome switch
        {
            CorpusOutcome.Succeeded => "succeeded",
            CorpusOutcome.CanonicalFailed => $"the imported project is not canonical:{Environment.NewLine}{report.Format.Describe()}",
            CorpusOutcome.RoundTripFailed => $"NSchema found differences against its own description of the database:{Environment.NewLine}{report.Verification.Describe()}",
            CorpusOutcome.RebuildFailed => $"the schema NSchema rendered was not the schema it described:{Environment.NewLine}{report.RebuildVerification.Describe()}",
            CorpusOutcome.FidelityFailed => $"the engine's own account of the rebuild differs from the source:{Environment.NewLine}{string.Join(Environment.NewLine, report.EngineTestimony ?? [])}",
            _ => $"the schema would not come apart again:{Environment.NewLine}{report.TeardownVerification.Describe()}",
        };
        report.Outcome.ShouldBe(expectation.Outcome,
            $"'{corpusName}' on '{engineName}' expected {expectation.Outcome} but {evidence}" +
            $"{Environment.NewLine}If the engine's capability changed, update the manifest; otherwise fix it.");

        // The verdicts held; pin the artifacts. A diff here always means a plan or a diagnostic changed.
        await Verify(report.Render()).UseTextForParameters($"{corpusName}.{engineName}");
    }
}
