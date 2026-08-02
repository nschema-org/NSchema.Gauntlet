using System.Collections.Concurrent;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services;

/// <summary>
/// The engines alive in this run.
/// </summary>
/// <remarks>
/// One fleet is shared by the whole assembly, so an engine is built and started at most once however many
/// cases ask for it — and never at all if no case does.
/// </remarks>
public sealed class EngineFleet : IAsyncLifetime
{
    private readonly ConcurrentDictionary<string, Lazy<Engine>> _engines = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the named engine, building it on first request.
    /// </summary>
    public Engine Get(string name) => _engines
        .GetOrAdd(name, key => new Lazy<Engine>(() => EngineCatalog.Get(key), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var engine in _engines.Values)
        {
            await engine.Value.DisposeAsync();
        }

        _engines.Clear();
    }
}
