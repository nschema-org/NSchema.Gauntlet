namespace NSchema.Gauntlet.Model;

/// <summary>
/// A database engine the gauntlet can run cases against.
/// </summary>
public abstract class Engine : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _started;

    /// <summary>
    /// The schema a case's objects land in on this engine.
    /// </summary>
    public abstract string DefaultSchema { get; }

    /// <summary>
    /// Creates an isolated, empty database for one case to run against.
    /// </summary>
    public async Task<EngineDatabase> CreateDatabase(string name, CancellationToken cancellationToken = default)
    {
        await EnsureStarted(cancellationToken);
        return await Provision(name, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_started)
        {
            await Stop();
        }

        _gate.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Brings the engine up. Called once, before the first database is provisioned.
    /// </summary>
    protected virtual Task Start(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Takes the engine down. Called only if it was started.
    /// </summary>
    protected virtual ValueTask Stop() => ValueTask.CompletedTask;

    /// <summary>
    /// Hands out one case's database, with the engine known to be up.
    /// </summary>
    protected abstract Task<EngineDatabase> Provision(string name, CancellationToken cancellationToken);

    private async Task EnsureStarted(CancellationToken cancellationToken)
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
