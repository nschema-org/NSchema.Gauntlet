using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Runner;
using NSchema.Gauntlet.Services;

namespace NSchema.Gauntlet.Tests;

public sealed class ScenarioTests(GauntletRun run)
{
    [Theory]
    [MemberData(nameof(GauntletMatrix.ScenariosAndEngines), MemberType = typeof(GauntletMatrix))]
    public async Task Scenarios(ScenarioName scenarioName, EngineName engineName)
    {
        // Arrange
        var runner = new ScenarioRunner(run.Cli);
        var scenario = run.Scenarios.Get(scenarioName);
        var engine = run.Engines.Get(engineName);
        var database = await engine.CreateDatabase(scenario.Name.Value, TestContext.Current.CancellationToken);
        var project = run.Project(database);

        // Act
        var result = await runner.Run(scenario, database, project, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse(result.Describe());
        var report = result.Value;

        scenario.Expectations.TryGetValue(engineName, out var expectation).ShouldBeTrue(
            $"Scenario '{scenarioName}' sets no expectation for '{engineName}'. " +
            $"Declare the expectation in the scenario manifest.");

        switch (expectation.Outcome == ScenarioOutcome.Succeeded)
        {
            case false when expectation.Because is null:
                throw new Exception($"'{scenarioName}' expects '{engineName}' to block but declares no reason; say which capability gap this documents.");
            case true when expectation.Because is not null:
                throw new Exception($"'{scenarioName}' expects '{engineName}' to apply; remove the leftover reason from the manifest.");
        }

        var evidence = report.Failure is { } failure
            ? $"failure at '{failure.Name}':{Environment.NewLine}{failure.Result.Describe()}"
            : "succeeded";
        report.Outcome.ShouldBe(expectation.Outcome,
            $"'{scenarioName}' on '{engineName}' expected {expectation.Outcome} but {evidence}" +
            $"{Environment.NewLine}If the engine's capability changed, update the manifest; otherwise fix it.");

        // Whatever the outcome, the database must settle where the scenario leaves it.
        var expected = report.Outcome == ScenarioOutcome.Succeeded
            ? "the change applied, but the database did not settle on the declared schema"
            : "the change was refused, but the database did not survive it intact";
        report.Converged.ShouldBeTrue($"{expected}:{Environment.NewLine}{report.Verification.Describe()}");

        // The verdicts held; pin the artifacts. A diff here always means the SQL or a diagnostic changed.
        await Verify(report.Render()).UseTextForParameters($"{scenarioName}.{engineName}");
    }
}
