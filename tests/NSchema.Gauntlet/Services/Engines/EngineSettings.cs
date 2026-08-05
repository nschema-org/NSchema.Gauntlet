namespace NSchema.Gauntlet.Services.Engines;

/// <summary>
/// The base settings for all database engines.
/// </summary>
public abstract class EngineSettings
{
    /// <summary>
    /// The plugin to use when connecting to a database of this engine.
    /// </summary>
    public required PluginSettings Plugin { get; init; }
}
