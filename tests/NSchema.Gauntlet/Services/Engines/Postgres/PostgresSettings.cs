namespace NSchema.Gauntlet.Services.Engines.Postgres;

/// <summary>
/// Configures one database engine provider..
/// </summary>
public sealed class PostgresSettings
{
    /// <summary>
    /// The container image.
    /// </summary>
    public required string Image { get; init; }

    /// <summary>
    /// The NSchema provider a project declares to reach this engine.
    /// </summary>
    public required PluginSettings Plugin { get; init; }
}
