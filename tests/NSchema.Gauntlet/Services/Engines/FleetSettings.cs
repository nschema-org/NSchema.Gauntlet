using NSchema.Gauntlet.Services.Engines.Postgres;
using NSchema.Gauntlet.Services.Engines.Sqlite;
using NSchema.Gauntlet.Services.Engines.SqlServer;

namespace NSchema.Gauntlet.Services.Engines;

/// <summary>
/// The settings for the database engines.
/// </summary>
public sealed class FleetSettings
{
    /// <summary>
    /// The Postgres settings.
    /// </summary>
    public required PostgresSettings Postgres { get; init; }

    /// <summary>
    /// The SQLite settings.
    /// </summary>
    public required SqliteSettings Sqlite { get; init; }

    /// <summary>
    /// The SQL Server settings.
    /// </summary>
    public required SqlServerSettings SqlServer { get; init; }
}
