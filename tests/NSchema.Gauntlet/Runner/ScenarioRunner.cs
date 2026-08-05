using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Runner;

/// <summary>
/// Runs a scenario against a live database.
/// </summary>
public sealed class ScenarioRunner(NSchemaClient nSchema)
{
    /// <summary>
    /// Runs the given scenario against the given database and returns the result.
    /// </summary>
    public async Task<ErrorOr<ScenarioResult>> Run(Scenario scenario, Database database, Project project, CancellationToken ct)
    {
        // Restore plugins.
        var restore = await nSchema.Init(project.Directory, ct);
        if (restore.IsError)
        {
            return restore.Errors;
        }

        // First refresh to populate the state store.
        var snapshot = await nSchema.Refresh(project.Directory, ct);
        if (snapshot.IsError)
        {
            return snapshot.Errors;
        }

        // Deploy the starting schemas.
        var bootstrapDdl = database.Localize(scenario.BootstrapNsql);
        project.SetSchema(bootstrapDdl);
        var bootstrap = await nSchema.Apply(project.Directory, DestructiveActionPolicy.Allow, ct);
        if (!bootstrap.Succeeded)
        {
            // Bootstrap failed, so try to reset the state and report the result.
            project.ClearSchema();
            var reset = await nSchema.Refresh(project.Directory, ct);
            if (reset.IsError)
            {
                return reset.Errors;
            }

            var failedStage = new ScenarioStage(StageName.Bootstrap, bootstrap);
            return new ScenarioResult
            {
                Stages = [failedStage],
                Failure = failedStage,
                Verification = await nSchema.Plan(project.Directory, DestructiveActionPolicy.Error, detailedExitCode: true, ct),
            };
        }

        // Seed the scenario data if there is any.
        if (scenario.SeedSql is { } seed)
        {
            await database.Execute(seed, ct);
        }

        // The change under test: plan it, then attempt it.
        var target = database.Localize(scenario.ScenarioNsql);
        project.SetSchema(target);
        var plan = await nSchema.Plan(project.Directory, scenario.DestructiveActions, detailedExitCode: false, ct);
        var apply = await nSchema.Apply(project.Directory, scenario.DestructiveActions, ct);

        // A refusal is an outcome, not an error: what it has to prove is that the database was left
        // where it started, so a refused change is verified against the bootstrap state.
        project.SetSchema(apply.Succeeded ? target : bootstrapDdl);
        var recapture = await nSchema.Refresh(project.Directory, ct);
        if (recapture.IsError)
        {
            return recapture.Errors;
        }

        return new ScenarioResult
        {
            Stages = [
                new ScenarioStage(StageName.Plan, plan),
                new ScenarioStage(StageName.Apply, apply)
            ],
            Failure = apply.Succeeded ? null : new ScenarioStage(StageName.Apply, apply),
            Verification = await nSchema.Plan(project.Directory, DestructiveActionPolicy.Error, detailedExitCode: true, ct),
        };
    }
}
