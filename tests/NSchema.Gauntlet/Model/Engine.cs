namespace NSchema.Gauntlet.Model;

/// <summary>
/// A database engine the gauntlet can run cases against.
/// </summary>
/// <remarks>
/// An engine starts on first use and stays up for the rest of the run, so a filtered run only pays for the
/// engines it touches. Its lifecycle is <see cref="IAsyncDisposable"/> rather than a test-framework
/// lifetime because the model does not know it is being run by a test framework.
/// </remarks>
public abstract class Engine(string name) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _started;

    /// <summary>
    /// The engine's name, as it appears in the matrix and in per-engine case files.
    /// </summary>
    public string Name { get; } = name;

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
