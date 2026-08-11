using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Services.Coverage;

/// <summary>
/// Plans, and records into <paramref name="ledger"/> what the plan turned out to perform.
/// </summary>
public sealed class PlanObserver(NSchemaClient cli, ActionLedger ledger)
{
    /// <summary>
    /// Plans against <paramref name="database"/> and returns that plan, recording what it performs.
    /// </summary>
    public async Task<CliResult> Plan(
        Database database,
        string projectDirectory,
        DestructiveActionPolicy destructiveActions,
        bool detailedExitCode,
        CancellationToken ct)
    {
        var result = await cli.Plan(projectDirectory, destructiveActions, detailedExitCode, ct);
        await Observe(database, projectDirectory, destructiveActions, ct);
        return result;
    }

    /// <summary>
    /// Records what a plan would perform, without the run needing the plan itself.
    /// </summary>
    public async Task Observe(
        Database database,
        string projectDirectory,
        DestructiveActionPolicy destructiveActions,
        CancellationToken ct)
    {
        // Outside the project: the saved plan is not one of the project's files, and a stray artifact in there is
        // one more thing for a later step to trip over.
        var planFile = Path.Combine(Path.GetTempPath(), $"nschema-coverage-{Guid.NewGuid():N}.json");

        try
        {
            await cli.Plan(projectDirectory, destructiveActions, detailedExitCode: false, planFile, ct);
            ledger.Record(database.Engine, PlanActions.Read(planFile));
        }
        finally
        {
            File.Delete(planFile);
        }
    }
}
