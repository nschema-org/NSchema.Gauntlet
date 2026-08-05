namespace NSchema.Gauntlet.Services.Engines.Postgres;

/// <summary>
/// The settings for the Postgres engine.
/// </summary>
public sealed class PostgresSettings : EngineSettings
{
    /// <summary>
    /// The container image to host the database in.
    /// </summary>
    public required string Image { get; init; }
}
