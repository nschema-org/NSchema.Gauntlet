namespace NSchema.Gauntlet.Services.Engines.Postgres;

/// <summary>
/// The settings for the postgres image.
/// </summary>
public sealed class PostgresSettings
{
    /// <summary>
    /// The container image to host the database in.
    /// </summary>
    public required string Image { get; init; }

    /// <summary>
    ///  The plugin settings to connect to postgres.
    /// </summary>
    public required PluginSettings Plugin { get; init; }
}
