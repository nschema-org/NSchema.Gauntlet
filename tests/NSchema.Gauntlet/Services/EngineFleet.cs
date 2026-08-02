using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Postgres;
using NSchema.Gauntlet.Services.Sqlite;

namespace NSchema.Gauntlet.Services;

/// <summary>
/// The engines alive in this run.
/// </summary>
public sealed class EngineFleet : IAsyncDisposable
{
    private readonly Dictionary<string, Lazy<Engine>> _engines = new(StringComparer.Ordinal);

    public EngineFleet(GauntletSettings settings)
    {
        _engines.Add("postgres", new Lazy<Engine>(() => new PostgresEngine("postgres", settings.Engine("postgres"))));
        _engines.Add("sqlite", new Lazy<Engine>(() => new SqliteEngine("sqlite", settings.Engine("sqlite"))));
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
