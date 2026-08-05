using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Runner;

/// <summary>
/// Puts a schema that already exists through the whole round trip: can NSchema describe it, rebuild it
/// somewhere else, and take it away again?
/// </summary>
/// <remarks>
/// Rebuilding needs a second database, so the runner owns the databases and projects rather than being
/// handed them: a case is one schema, not one database.
/// </remarks>
public sealed class CorpusRunner(NSchemaClient nSchema, Func<Database, Project> project)
{
    public async Task<ErrorOr<CorpusResult>> Run(Corpus corpus, DatabaseEngine engine, CancellationToken ct)
    {

        var source = await engine.CreateDatabase($"{corpus.Name.Value}_source", ct);
        var sourceProject = project(source);

        // Establish the schema with the engine's own DDL, bypassing NSchema entirely. What is being
        // tested is whether NSchema can describe a database it did not create.
        try
        {
            var ddl = corpus.Ddl[engine.Name];
            await source.Execute(ddl, ct);
        }
        catch (Exception exception)
        {
            return Error.Failure("seed", exception.Message);
        }

        if (await nSchema.Init(sourceProject.Directory, ct) is { IsError: true } restore)
        {
            return restore.Errors;
        }

        // Describe — write the database out as a project, then read it back.
        if (await nSchema.Import(sourceProject.Directory, ct) is { IsError: true } import)
        {
            return import.Errors;
        }

        var format = await nSchema.Format(sourceProject.Directory, ct);
        if (await nSchema.Refresh(sourceProject.Directory, ct) is { IsError: true } capture)
        {
            return capture.Errors;
        }

        // A schema NSchema did not create is unmanaged, so the first plan adopts it. Adoption is bookkeeping
        // rather than a difference, so it is applied and then planned again: what this claims is that nothing
        // is left once the database is NSchema's to manage.
        var adoption = await nSchema.Plan(sourceProject.Directory, DestructiveActionPolicy.Error, detailedExitCode: false, ct);

        if (await nSchema.Apply(sourceProject.Directory, DestructiveActionPolicy.Error, ct) is { Succeeded: false } adopt)
        {
            return Error.Failure("adopt", adopt.Describe());
        }

        var verification = await nSchema.Plan(sourceProject.Directory, DestructiveActionPolicy.Error, detailedExitCode: true, ct);

        // Rebuild — the same declarations against an empty database. Nothing above ever ran a statement the
        // dialect rendered; this is what proves the SQL NSchema writes builds the schema it described.
        var target = await engine.CreateDatabase($"{corpus.Name.Value}_target", ct);
        var targetProject = project(target);
        targetProject.SetSchema(sourceProject.GetSchema());

        if (await nSchema.Init(targetProject.Directory, ct) is { IsError: true } targetRestore)
        {
            return targetRestore.Errors;
        }

        if (await nSchema.Refresh(targetProject.Directory, ct) is { IsError: true } targetCapture)
        {
            return targetCapture.Errors;
        }

        var create = await nSchema.Apply(targetProject.Directory, DestructiveActionPolicy.Error, ct);
        var created = create;
        if (create.Succeeded)
        {
            var settled = await Settled(targetProject.Directory, ct);
            if (settled.IsError)
            {
                return settled.Errors;
            }
            created = settled.Value;
        }

        // The oracle outside NSchema: both databases' own accounts of their schemas, compared. Every leg
        // above compares NSchema to NSchema, so a consistent introspection error passes them all; the
        // engine's catalog is the one witness NSchema cannot influence.
        IReadOnlyList<string>? testimony = null;
        if (create.Succeeded && created.Succeeded)
        {
            testimony = Testimony.Differences(await source.Catalog(ct), await target.Catalog(ct));
        }

        // Take away — declaring nothing makes the target an empty database, which for a schema with a foreign
        // key graph is the only test of the order things are dropped in.
        targetProject.ClearSchema();

        var teardown = await nSchema.Apply(targetProject.Directory, DestructiveActionPolicy.Allow, ct);
        var emptied = teardown;
        if (teardown.Succeeded)
        {
            var settled = await Settled(targetProject.Directory, ct);
            if (settled.IsError)
            {
                return settled.Errors;
            }
            emptied = settled.Value;
        }

        return new CorpusResult
        {
            Format = format,
            Adoption = adoption,
            Verification = verification,
            Rebuild = create,
            RebuildVerification = created,
            EngineTestimony = testimony,
            Teardown = teardown,
            TeardownVerification = emptied,
        };
    }

    // Recapture the live schema and confirm the project has nothing left to say about it.
    private async Task<ErrorOr<CliResult>> Settled(string projectDirectory, CancellationToken ct)
    {
        if (await nSchema.Refresh(projectDirectory, ct) is { IsError: true } recapture)
        {
            return recapture.Errors;
        }

        return await nSchema.Plan(projectDirectory, DestructiveActionPolicy.Allow, detailedExitCode: true, ct);
    }
}
