using Microsoft.Data.Sqlite;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Engines.Sqlite;

/// <summary>
/// The SQLite engine.
/// </summary>
public sealed class SqliteEngine(SqliteSettings settings, string tempDirectory) : DatabaseEngine
{
    public static readonly EngineName Name = EngineName.From("sqlite");
    private readonly string _directory = Path.Combine(tempDirectory, "sqlite", Path.GetRandomFileName());

    /// <remarks>
    /// SQLite's primary database is always <c>main</c>; it has no other schema.
    /// </remarks>
    protected override string DefaultSchema => "main";

    protected override ValueTask<Database> Provision(string caseName, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_directory))
        {
            Directory.CreateDirectory(_directory);
        }

        var file = Path.Combine(_directory, $"{caseName}.db");
        var builder = new SqliteConnectionStringBuilder { DataSource = file };
        var database = new SqliteDatabase(this, settings.Plugin, builder.ConnectionString);
        return ValueTask.FromResult<Database>(database);
    }

    public override ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
        return ValueTask.CompletedTask;
    }
}
