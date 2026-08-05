namespace NSchema.Gauntlet.Services.Engines.SqlServer;

/// <summary>
/// Configures the SQL Server engine.
/// </summary>
public sealed class SqlServerSettings : EngineSettings
{
    /// <summary>
    /// The container image.
    /// </summary>
    public required string Image { get; init; }
}
