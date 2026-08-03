using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Runner;
using NSchema.Gauntlet.Services;

namespace NSchema.Gauntlet.Tests;

public sealed class ScenarioTests(GauntletRun run)
{
    [Theory]
    [MemberData(nameof(GauntletMatrix.ScenariosAndEngines), MemberType = typeof(GauntletMatrix))]
    public async Task Scenarios(string name, string engineName)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = run.Scenarios.Get(name);
        var engine = run.Engines.Get(engineName);
        await using var database = await engine.CreateDatabase(name, ct);
        using var project = GauntletProject.Create(engine, database);

        // Act
        var outcome = await new ScenarioRunner(run.Cli, project).Run(database, scenario, ct);

        // Assert
        outcome.SetupFailure.ShouldBeNull(outcome.SetupFailure?.Describe());

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
