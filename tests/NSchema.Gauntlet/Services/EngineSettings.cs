namespace NSchema.Gauntlet.Services;

/// <summary>
/// Configures one database engine provider..
/// </summary>
public sealed class EngineSettings
{
    /// <summary>
    /// The container image, for engines that run in one.
    /// </summary>
    public string? Image { get; init; }

    /// <summary>
    /// The NSchema provider a project declares to reach this engine.
    /// </summary>
    public PluginSettings Plugin { get; init; } = new();

    /// <summary>
    /// The image, for an engine that cannot run without one.
    /// </summary>
    public string RequiredImage(string name) => Image ?? throw new InvalidOperationException($"Engine '{name}' needs an image configured in appsettings.json.");
}
