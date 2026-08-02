using NSchema.Gauntlet.Services.Engines.Postgres;
using NSchema.Gauntlet.Services.Engines.Sqlite;

namespace NSchema.Gauntlet.Services.Engines;

/// <summary>
/// The settings for the database engines.
/// </summary>
public sealed class EngineSettings
{
    /// <summary>
    /// The container image, for engines that run in one.
    /// </summary>
    public required PostgresSettings Postgres { get; init; }

    /// <summary>
    /// The NSchema provider a project declares to reach this engine.
    /// </summary>
    public required SqliteSettings Sqlite { get; init; }
}
