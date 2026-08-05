using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Engines.Postgres;
using NSchema.Gauntlet.Services.Engines.Sqlite;
using NSchema.Gauntlet.Services.Engines.SqlServer;

namespace NSchema.Gauntlet.Services.Engines;

/// <summary>
/// The engines alive in this run.
/// </summary>
public sealed class EngineFleet : IAsyncDisposable
{
    private readonly Dictionary<EngineName, Lazy<DatabaseEngine>> _engines = new();

    public EngineFleet(FleetSettings settings, string tempDirectory)
    {
        _engines.Add(PostgresEngine.Name, new Lazy<DatabaseEngine>(() => new PostgresEngine(settings.Postgres)));
        _engines.Add(SqliteEngine.Name, new Lazy<DatabaseEngine>(() => new SqliteEngine(settings.Sqlite, tempDirectory)));
        _engines.Add(SqlServerEngine.Name, new Lazy<DatabaseEngine>(() => new SqlServerEngine(settings.SqlServer)));
    }

    /// <summary>
    /// The registered engine names, in matrix order.
    /// </summary>
    public IEnumerable<EngineName> Names => _engines.Keys;

    /// <summary>
    /// Gets the named engine, building it on first request.
    /// </summary>
    public DatabaseEngine Get(EngineName name) => _engines[name].Value;

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
