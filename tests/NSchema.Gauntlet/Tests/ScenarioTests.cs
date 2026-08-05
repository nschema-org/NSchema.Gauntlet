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

        var declared = scenario.Limitations.TryGetValue(engineName, out var limitation);
        var report = outcome.Report(limitation);
        await Verify(report).UseTextForParameters($"{name}.{engineName}");

        // A limitation must be acknowledged/declared in the manifest.
        if (outcome.Blocked)
        {
            declared.ShouldBeTrue(
                $"'{name}' blocked on {engineName} with no declared limitation. " +
                $"If this is an engine capability gap, declare it in the scenario manifest, otherwise fix it.");
        }
        else
        {
            declared.ShouldBeFalse($"'{name}' no longer blocks on {engineName}; remove the stale limitation from the manifest.");
        }

        var expectation = outcome.Blocked
            ? "the change was refused, but the database did not survive it intact"
            : "the change applied, but the database did not settle on the declared schema";

        outcome.Converged.ShouldBeTrue($"{expectation}:{Environment.NewLine}{outcome.Verification?.Describe()}");
    }
}
