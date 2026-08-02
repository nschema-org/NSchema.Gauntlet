using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Sqlite;

/// <summary>
/// SQLite, one file per case.
/// </summary>
/// <remarks>
/// There is nothing to start, which is the point of the engine owning its own lifecycle: not every engine
/// is a container.
/// </remarks>
public sealed class SqliteEngine(string name, EngineSettings settings) : Engine(name)
{
    /// <remarks>
    /// SQLite's primary database is always <c>main</c>; it has no other schema.
    /// </remarks>
    public override string DefaultSchema => "main";

    protected override Task<EngineDatabase> Provision(string caseName, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "nschema-gauntlet-sqlite", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);

        var file = Path.Combine(directory, $"{caseName}.db");

        return Task.FromResult<EngineDatabase>(new SqliteEngineDatabase(file, directory, settings.Plugin, Name));
    }
}
