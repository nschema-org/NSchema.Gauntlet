namespace NSchema.Gauntlet.Services.Engines.Sqlite;

/// <summary>
/// The settings for the Sqlite engine.
/// </summary>
public sealed class SqliteSettings
{
    /// <summary>
    /// The plugin settings to connect to Sqlite.
    /// </summary>
    public PluginSettings Plugin { get; init; } = new();
}
