using Microsoft.Data.SqlClient;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Engines.SqlServer;

/// <summary>
/// A single SQL Server database.
/// </summary>
public sealed class SqlServerDatabase(SqlServerEngine engine, PluginSettings plugin, string connectionString) : Database(engine, plugin, connectionString)
{
    protected override async Task ExecuteCore(Sql sql, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // GO is the client tools' batch separator, not T-SQL: a CREATE VIEW or CREATE PROCEDURE must be
        // the only statement in its batch, so upstream scripts keep their separators and the harness
        // honours them the way sqlcmd would.
        foreach (var batch in Batches(sql.Value))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static IEnumerable<string> Batches(string script)
    {
        var batch = new List<string>();

        foreach (var line in script.Split('\n'))
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (batch.Any(l => !string.IsNullOrWhiteSpace(l)))
                {
                    yield return string.Join('\n', batch);
                }

                batch.Clear();
            }
            else
            {
                batch.Add(line);
            }
        }

        if (batch.Any(l => !string.IsNullOrWhiteSpace(l)))
        {
            yield return string.Join('\n', batch);
        }
    }

    public override async Task<IReadOnlyList<CatalogFact>> Catalog(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var facts = new List<CatalogFact>();

        foreach (var catalog in SqlServerCatalogs.All)
        {
            var exclude = catalog.Exclude.Concat(SqlServerCatalogs.Bookkeeping).ToHashSet(StringComparer.Ordinal);

            await using var command = connection.CreateCommand();
            command.CommandText = catalog.Sql;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var identity = reader.IsDBNull(0) ? "(null)" : reader.GetString(0);
                var json = reader.IsDBNull(1) ? "{}" : reader.GetString(1);
                var resolved = reader.FieldCount > 2 && !reader.IsDBNull(2) ? reader.GetString(2) : null;

                facts.AddRange(CatalogRow.Flatten(catalog.Name, identity, json, exclude, resolved));
            }
        }

        return facts;
    }
}
