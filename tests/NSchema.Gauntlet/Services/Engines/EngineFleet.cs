using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Engines.Postgres;
using NSchema.Gauntlet.Services.Engines.Sqlite;

namespace NSchema.Gauntlet.Services.Engines;

/// <summary>
/// The engines alive in this run.
/// </summary>
public sealed class EngineFleet : IAsyncDisposable
{
    private readonly Dictionary<string, Lazy<Engine>> _engines = new(StringComparer.Ordinal);

    public EngineFleet(EngineSettings settings)
    {
        _engines.Add(PostgresEngine.Name, new Lazy<Engine>(() => new PostgresEngine(settings.Postgres)));
        _engines.Add(SqliteEngine.Name, new Lazy<Engine>(() => new SqliteEngine(settings.Sqlite)));
    }

    /// <summary>
    /// The registered engine names, in matrix order.
    /// </summary>
    public IEnumerable<string> Names => _engines.Keys.OrderBy(name => name, StringComparer.Ordinal);

    /// <summary>
    /// Gets the named engine, building it on first request.
    /// </summary>
    public Engine Get(string name) => _engines[name].Value;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var engine in _engines.Values.Where(engine => engine.IsValueCreated))
        {
            await engine.Value.DisposeAsync();
        }
        _engines.Clear();
    }
}
