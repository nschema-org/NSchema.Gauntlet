using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Runner;

/// <summary>
/// Runs a scenario against a live database.
/// </summary>
public sealed class ScenarioRunner(NSchemaClient nSchema)
{
    /// <summary>
    /// Runs the given scenario against the given engine and returns the result.
    /// </summary>
    public async Task<ErrorOr<ScenarioResult>> Run(Scenario scenario, Database database, Project project, CancellationToken ct)
    {
        var setup = await Setup(scenario, project, database, ct);
        if (setup.IsError)
        {
            return setup.Errors;
        }

        // The change under test: plan it, then attempt it.
        project.SetSchema(scenario.ScenarioNsql);
        var plan = await nSchema.Plan(project.Directory, scenario.DestructiveActions, detailedExitCode: false, ct);
        var apply = await nSchema.Apply(project.Directory, scenario.DestructiveActions, ct);

        // A refusal is an outcome, not an error: what it has to prove is that the database was left
        // where it started, so a refused change is verified against the before state.
        project.SetSchema(apply.Succeeded ? scenario.ScenarioNsql : scenario.BootstrapNsql);
        var recapture = await nSchema.Refresh(project.Directory, ct);
        if (recapture.IsError)
        {
            return recapture.Errors;
        }

        return new ScenarioResult
        {
            Stages = [new ScenarioStage("plan", plan), new ScenarioStage("apply", apply)],
            Refusal = apply.Succeeded ? null : new ScenarioStage("apply", apply),
            Verification = await nSchema.Plan(project.Directory, DestructiveActionPolicy.Error, detailedExitCode: true, ct),
        };
    }

    private async Task<ErrorOr<Success>> Setup(Scenario scenario, Project project, Database database, CancellationToken ct)
    {
        // Restore plugins.
        var restore = await nSchema.Init(project.Directory, ct);
        if (restore.IsError)
        {
            return restore.Errors;
        }

        // First refresh to bootstrap the state.
        var snapshot = await nSchema.Refresh(project.Directory, ct);
        if (snapshot.IsError)
        {
            return snapshot.Errors;
        }

        // Establish the before state.
        project.SetSchema(scenario.BootstrapNsql);
        var before = await nSchema.Apply(project.Directory, DestructiveActionPolicy.Allow, ct);
        if (!before.Succeeded)
        {
            // Apply failed. Attempt a recovery reset.
            project.ClearSchema();
            var reset = await nSchema.Refresh(project.Directory,  ct);
            if (reset.IsError)
            {
                return reset.Errors;
            }

            var metadata = new Dictionary<string, object> {
                ["stage"] = new ScenarioStage("before", before),
                ["plan"] = await nSchema.Plan(project.Directory, DestructiveActionPolicy.Error, detailedExitCode: true, ct),
            };

            return Error.Failure("Setup.FailedBefore", "Error setting up before state.", metadata);
        }

        // Seed the scenario data if there is any.
        if (scenario.SeedSql is { } dataSql)
        {
            await database.Execute(dataSql, ct);
        }

        return Result.Success;
    }
}
