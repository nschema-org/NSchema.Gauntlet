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
        var outcome = await new ScenarioRunner(project).Run(database, scenario, ct);

        // Assert
        outcome.SetupFailure.ShouldBeNull(outcome.SetupFailure?.Describe());

        var report = outcome.Report();
        await Verify(report).UseTextForParameters($"{name}.{engineName}");

        var expectation = outcome.Blocked
            ? "the change was refused, but the database did not survive it intact"
            : "the change applied, but the database did not settle on the declared schema";

        outcome.Converged.ShouldBeTrue($"{expectation}:{Environment.NewLine}{outcome.Verification?.Describe()}");
    }
}
