using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Runner;

/// <summary>
/// Round-trips a schema that already exists: can NSchema describe it, and does it then agree with itself?
/// </summary>
public sealed class CorpusRunner(GauntletProject project)
{
    private readonly NSchemaCli _cli = new(project.Directory);

    public async Task<CorpusOutcome> Run(EngineDatabase database, string ddl, CancellationToken ct)
    {
        // Arrange — establish the schema with the engine's own DDL, bypassing NSchema entirely. What is being
        // tested is whether NSchema can describe a database it did not create.
        try
        {
            await database.Execute(ddl, ct);
        }
        catch (Exception exception)
        {
            return CorpusOutcome.Failed(new CliResult("seed", 1, string.Empty, exception.Message));
        }

        var restore = await _cli.Init(ct);
        if (!restore.Succeeded)
        {
            return CorpusOutcome.Failed(restore);
        }

        // Act — write the database out as a project, then read it back.
        var import = await _cli.Import(ct);
        if (!import.Succeeded)
        {
            return CorpusOutcome.Failed(import);
        }

        // Assert — the writer's output is already canonical, and the project describes the database exactly.
        var format = await _cli.Format(ct);

        var capture = await _cli.Refresh(ct);
        if (!capture.Succeeded)
        {
            return CorpusOutcome.Failed(capture);
        }

        // A schema NSchema did not create is unmanaged, so the first plan adopts it. Adoption is bookkeeping
        // rather than a difference, so it is applied and then planned again: what fidelity claims is that
        // nothing is left once the database is NSchema's to manage.
        var adoption = await _cli.Plan(DestructiveActionPolicy.Error, detailedExitCode: false, ct);

        var adopt = await _cli.Apply(DestructiveActionPolicy.Error, ct);
        if (!adopt.Succeeded)
        {
            return CorpusOutcome.Failed(adopt);
        }

        var verification = await _cli.Plan(DestructiveActionPolicy.Error, detailedExitCode: true, ct);

        return new CorpusOutcome
        {
            Import = import,
            Format = format,
            Adoption = adoption,
            Verification = verification,
        };
    }
}
