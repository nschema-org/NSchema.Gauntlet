namespace NSchema.Gauntlet.Model;

/// <summary>
/// A database engine the gauntlet can run cases against.
/// </summary>
public abstract class DatabaseEngine : IAsyncDisposable
{
    /// <summary>
    /// The token to replace with <see cref="DefaultSchema"/> when localizing SQL to run against a database that runs on this engine.
    /// </summary>
    private const string SchemaToken = "{schema}";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _started;

    /// <summary>
    /// The name of the engine.
    /// </summary>
    public abstract EngineName Name { get; }

    /// <summary>
    /// The schema a case's objects land in on this engine.
    /// </summary>
    protected abstract string DefaultSchema { get; }

    /// <summary>
    /// Creates an isolated, empty database for one case to run against.
    /// </summary>
    public async ValueTask<Database> CreateDatabase(string name, CancellationToken cancellationToken = default)
    {
        await EnsureStarted(cancellationToken);
        return await Provision(name, cancellationToken);
    }

    /// <summary>
    /// Localizes the given SQL for the current database engine by replacing <see cref="SchemaToken"/> with this engine's default schema.
    /// </summary>
    public Sql Localize(Sql sql)
    {
        var localizedSql = sql.Value.Replace(SchemaToken, DefaultSchema);
        return Sql.From(localizedSql);
    }

    /// <summary>
    /// Localizes the given NSQL for the current database engine by replacing <see cref="SchemaToken"/> with this engine's default schema.
    /// </summary>
    public Nsql Localize(Nsql nsql) => Nsql.From(nsql.Value.Replace(SchemaToken, DefaultSchema));

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        if (_started)
        {
            await Stop();
        }

        _gate.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Brings the engine up.
    /// </summary>
    protected virtual ValueTask Start(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>
    /// Takes the engine down.
    /// </summary>
    protected virtual ValueTask Stop() => ValueTask.CompletedTask;

    /// <summary>
    /// Provisions a single database, with the engine known to be up.
    /// </summary>
    protected abstract ValueTask<Database> Provision(string name, CancellationToken cancellationToken);

    private async ValueTask EnsureStarted(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!_started)
            {
                await Start(cancellationToken);
                _started = true;
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
