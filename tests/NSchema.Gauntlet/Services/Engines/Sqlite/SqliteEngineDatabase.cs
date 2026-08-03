using Microsoft.Data.Sqlite;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Engines.Sqlite;

/// <inheritdoc />
public sealed class SqliteEngineDatabase(string file, string directory, PluginSettings plugin, string label) : EngineDatabase
{
    private string ConnectionString => $"Data Source={file}";

    public override string ConfigurationSql =>
        $"""
         {plugin.Declaration(label)}

         DATABASE {label} (
           connection_string = '{ConnectionString.Replace("'", "''")}'
         );
         """;

    public override async Task Execute(string sql, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task<IReadOnlyList<string>> Inventory(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // The catalog's account of the schema. sqlite_master's sql column is the original text, which two
        // faithful databases may format differently, so objects testify by kind and name and columns by shape.
        command.CommandText = """
            SELECT type || ' | ' || name
            FROM sqlite_master
            WHERE type IN ('table', 'view', 'trigger', 'index') AND name NOT LIKE 'sqlite_%'
            UNION ALL
            SELECT 'column | ' || m.name || '.' || p.name || ' | ' || lower(coalesce(p.type, ''))
                   || ' notnull=' || p."notnull" || ' default=' || coalesce(p.dflt_value, '')
            FROM sqlite_master m JOIN pragma_table_info(m.name) p
            WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%'
            ORDER BY 1
            """;

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    public override ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Already gone.
        }

        return ValueTask.CompletedTask;
    }
}
