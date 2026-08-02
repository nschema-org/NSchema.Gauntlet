namespace NSchema.Gauntlet.Services.Engines.SqlServer;

/// <summary>
/// Configures the SQL Server engine.
/// </summary>
public sealed class SqlServerSettings
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
