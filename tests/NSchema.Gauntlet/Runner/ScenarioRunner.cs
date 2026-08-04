using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Runner;

/// <summary>
/// Runs a scenario against a live database and reports what NSchema did.
/// </summary>
public sealed class ScenarioRunner(CliInstallation cli, GauntletProject project)
{
    private readonly NSchemaCli _cli = new(cli, project.Directory);

    public async Task<ScenarioOutcome> Run(EngineDatabase database, Scenario scenario, CancellationToken ct)
    {
        // Arrange — resolve the declared plugins, then establish the before state.
        var restore = await _cli.Init(ct);
        if (!restore.Succeeded)
        {
            return ScenarioOutcome.Failed(restore);
        }

        // Take a first snapshot of the database.
        var adopt = await _cli.Refresh(ct);
        if (!adopt.Succeeded)
        {
            return ScenarioOutcome.Failed(adopt);
        }

        project.SetSchema(scenario.BeforeNsql ?? string.Empty);

        var seed = await _cli.Apply(DestructiveActionPolicy.Allow, ct);
        if (!seed.Succeeded)
        {
            return ScenarioOutcome.Failed(seed);
        }

        if (scenario.DataSql is { } dataSql)
        {
            await database.Execute(dataSql, ct);

            // The rows are invisible to a schema refresh, but the state must still match what was just applied.
            var capture = await _cli.Refresh(ct);
            if (!capture.Succeeded)
            {
                return ScenarioOutcome.Failed(capture);
            }
        }

        // Act — plan and apply the change under test.
        project.SetSchema(scenario.AfterNsql ?? string.Empty);

        var plan = await _cli.Plan(scenario.DestructiveActions, false, ct);
        var apply = await _cli.Apply(scenario.DestructiveActions, ct);

        // Assert. A refusal is an outcome, not an error: what it has to prove is that the database was left
        // where it started, so the before state is what a blocked scenario is verified against.
        project.SetSchema((apply.Succeeded ? scenario.AfterNsql : scenario.BeforeNsql) ?? string.Empty);

        await _cli.Refresh(ct);
        var verification = await _cli.Plan(DestructiveActionPolicy.Error, true, ct);

        return new ScenarioOutcome
        {
            Plan = plan,
            Apply = apply,
            Verification = verification,
        };
    }
}
