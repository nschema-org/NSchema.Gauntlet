using NSchema.Gauntlet.Services.Engines.Postgres;
using NSchema.Gauntlet.Services.Engines.Sqlite;
using NSchema.Gauntlet.Services.Engines.SqlServer;

namespace NSchema.Gauntlet.Services.Engines;

/// <summary>
/// The settings for the database engines.
/// </summary>
public sealed class EngineSettings
{
    /// <summary>
    /// The settings for the postgres engine.
    /// </summary>
    public required PostgresSettings Postgres { get; init; }

    /// <summary>
    /// The settings for the SQL Server engine.
    /// </summary>
    public required SqlServerSettings SqlServer { get; init; }

    /// <summary>
    /// The settings for the SQLite engine.
    /// </summary>
    public required SqliteSettings Sqlite { get; init; }
}
