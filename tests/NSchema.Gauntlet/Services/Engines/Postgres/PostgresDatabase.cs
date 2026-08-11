using Npgsql;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Engines.Postgres;

/// <summary>
/// A single Postgres database.
/// </summary>
public sealed class PostgresDatabase(PostgresEngine engine, PluginSettings plugin, string connectionString) : Database(engine, plugin, connectionString)
{
    protected override async Task ExecuteCore(Sql sql, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task<IReadOnlyList<CatalogFact>> Catalog(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var facts = new List<CatalogFact>();

        foreach (var (name, sql) in PostgresCatalogs.All)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var identity = reader.IsDBNull(0) ? "(null)" : reader.GetString(0);
                var json = reader.IsDBNull(1) ? "{}" : reader.GetString(1);
                facts.AddRange(CatalogRow.Flatten(name, identity, json));
            }
        }

        return facts;
    }
}
