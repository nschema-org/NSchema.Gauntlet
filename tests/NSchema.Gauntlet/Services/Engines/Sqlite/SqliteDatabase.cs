using Microsoft.Data.Sqlite;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Engines.Sqlite;

/// <summary>
/// A single Sqlite database.
/// </summary>
public sealed class SqliteDatabase(SqliteEngine engine, PluginSettings plugin, string connectionString) : Database(engine, plugin, connectionString)
{
    /// <summary>The suffix marking a selection that replaces another column's value.</summary>
    private const string Override = "__as";

    protected override async Task ExecuteCore(Sql sql, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Sqlite's own account of this database, one fact per column of every pragma it will answer.
    /// </summary>
    /// <remarks>
    /// The columns are enumerated from the reader rather than named by the query, so a pragma that gains one in a
    /// later Sqlite is compared without this file changing. See <see cref="SqliteCatalogs"/> for what is read and
    /// what is deliberately left out.
    /// </remarks>
    public override async Task<IReadOnlyList<CatalogFact>> Catalog(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var facts = new List<CatalogFact>();


        foreach (var catalog in SqliteCatalogs.All)
        {
            var exclude = catalog.Exclude.ToHashSet(StringComparer.Ordinal);

            await using var command = connection.CreateCommand();
            command.CommandText = catalog.Sql;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var identity = string.Join('.', catalog.Identity.Select(column => Value(reader, reader.GetOrdinal(column))));

                // A '<column>__as' selection replaces that column's value, which is how a value is normalised
                // without the query having to name every column it is not normalising.
                var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var column = 0; column < reader.FieldCount; column++)
                {
                    if (reader.GetName(column).EndsWith(Override, StringComparison.Ordinal))
                    {
                        overrides[reader.GetName(column)[..^Override.Length]] = Value(reader, column);
                    }
                }

                for (var column = 0; column < reader.FieldCount; column++)
                {
                    var name = reader.GetName(column);

                    // The identity is what a fact is about, so repeating it as an attribute of itself says nothing.
                    if (exclude.Contains(name)
                        || name.EndsWith(Override, StringComparison.Ordinal)
                        || catalog.Identity.Contains(name, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    var value = overrides.TryGetValue(name, out var replacement) ? replacement : Value(reader, column);
                    facts.Add(new CatalogFact(catalog.Name, identity, name, value));
                }
            }
        }

        return facts;
    }

    // Null is a value a column can hold rather than an absence, so it is spelled out: a default going from
    // something to nothing has to read as a change, not as an attribute that stopped existing.
    private static string Value(System.Data.Common.DbDataReader reader, int column) =>
        reader.IsDBNull(column) ? "NULL" : reader.GetValue(column).ToString() ?? string.Empty;
}
