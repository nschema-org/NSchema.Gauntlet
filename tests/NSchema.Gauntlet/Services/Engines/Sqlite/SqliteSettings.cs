namespace NSchema.Gauntlet.Services.Engines.Sqlite;

/// <summary>
/// Configures one database engine provider..
/// </summary>
public sealed class SqliteSettings
{
    /// <summary>
    /// The NSchema provider a project declares to reach this engine.
    /// </summary>
    public PluginSettings Plugin { get; init; } = new();
}
