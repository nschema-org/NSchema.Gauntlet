using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Runner;

/// <summary>
/// Puts a schema that already exists through the whole round trip: can NSchema describe it, rebuild it
/// somewhere else, and take it away again?
/// </summary>
/// <remarks>
/// Rebuilding needs a second database, so the runner owns the databases and projects rather than being handed
/// them: a case is one schema, not one database.
/// </remarks>
public sealed class CorpusRunner(CliInstallation cli, Engine engine)
{
    public async Task<CorpusOutcome> Run(string name, string ddl, CancellationToken ct)
    {
        await using var source = await engine.CreateDatabase($"{name}_source", ct);
        using var sourceProject = GauntletProject.Create(engine, source);
        var nschema = new NSchemaCli(cli, sourceProject.Directory);

        // Arrange — establish the schema with the engine's own DDL, bypassing NSchema entirely. What is being
        // tested is whether NSchema can describe a database it did not create.
        try
        {
            await source.Execute(ddl, ct);
        }
        catch (Exception exception)
        {
            return CorpusOutcome.Failed(new CliResult("seed", 1, string.Empty, exception.Message));
        }

        if (await nschema.Init(ct) is { Succeeded: false } restore)
        {
            return CorpusOutcome.Failed(restore);
        }

        // Describe — write the database out as a project, then read it back.
        var import = await nschema.Import(ct);
        if (!import.Succeeded)
        {
            return CorpusOutcome.Failed(import);
        }

        var format = await nschema.Format(ct);
        if (await nschema.Refresh(ct) is { Succeeded: false } capture)
        {
            return CorpusOutcome.Failed(capture);
        }

        // A schema NSchema did not create is unmanaged, so the first plan adopts it. Adoption is bookkeeping
        // rather than a difference, so it is applied and then planned again: what this claims is that nothing
        // is left once the database is NSchema's to manage.
        var adoption = await nschema.Plan(DestructiveActionPolicy.Error, detailedExitCode: false, ct);

        if (await nschema.Apply(DestructiveActionPolicy.Error, ct) is { Succeeded: false } adopt)
        {
            return CorpusOutcome.Failed(adopt);
        }

        var verification = await nschema.Plan(DestructiveActionPolicy.Error, detailedExitCode: true, ct);

        // Rebuild — the same declarations against an empty database. Nothing above ever ran a statement the
        // dialect rendered; this is what proves the SQL NSchema writes builds the schema it described.
        await using var target = await engine.CreateDatabase($"{name}_target", ct);
        using var targetProject = GauntletProject.Create(engine, target);
        targetProject.TakeSchemaFrom(sourceProject);
        var rebuild = new NSchemaCli(cli, targetProject.Directory);

        if (await rebuild.Init(ct) is { Succeeded: false } targetRestore)
        {
            return CorpusOutcome.Failed(targetRestore);
        }

        if (await rebuild.Refresh(ct) is { Succeeded: false } targetCapture)
        {
            return CorpusOutcome.Failed(targetCapture);
        }

        var create = await rebuild.Apply(DestructiveActionPolicy.Error, ct);
        var created = create.Succeeded ? await Settled(rebuild, ct) : create;

        // Take away — declaring nothing makes the target an empty database, which for a schema with a foreign
        // key graph is the only test of the order things are dropped in.
        targetProject.ClearSchema();

        var teardown = await rebuild.Apply(DestructiveActionPolicy.Allow, ct);
        var emptied = teardown.Succeeded ? await Settled(rebuild, ct) : teardown;

        return new CorpusOutcome
        {
            Import = import,
            Format = format,
            Adoption = adoption,
            Verification = verification,
            Rebuild = create,
            RebuildVerification = created,
            Teardown = teardown,
            TeardownVerification = emptied,
        };
    }

    // Recapture the live schema and confirm the project has nothing left to say about it.
    private static async Task<CliResult> Settled(NSchemaCli cli, CancellationToken ct)
    {
        await cli.Refresh(ct);

        return await cli.Plan(DestructiveActionPolicy.Allow, detailedExitCode: true, ct);
    }
}
