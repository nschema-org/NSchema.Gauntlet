namespace NSchema.Gauntlet.Model;

/// <summary>
/// One case's isolated database.
/// </summary>
public abstract class EngineDatabase : IAsyncDisposable
{
    /// <summary>
    /// The PLUGIN and DATABASE statements a project needs to reach this database.
    /// </summary>
    public abstract string ConfigurationSql { get; }

    /// <summary>
    /// Runs engine-native DDL directly, bypassing NSchema — how corpus schemas and data seeds are established.
    /// </summary>
    public abstract Task Execute(string sql, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract ValueTask DisposeAsync();
}
