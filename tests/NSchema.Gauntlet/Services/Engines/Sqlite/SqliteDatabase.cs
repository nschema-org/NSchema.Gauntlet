using Microsoft.Data.Sqlite;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Engines.Sqlite;

/// <summary>
/// A single Sqlite database.
/// </summary>
public sealed class SqliteDatabase(SqliteEngine engine, PluginSettings plugin, string connectionString) : Database(engine, plugin, connectionString)
{
    protected override async Task ExecuteCore(Sql sql, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task<IReadOnlyList<string>> Catalog(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // The catalog's account of the schema. Sqlite ignores a declared type's spelling — semantics are its
        // documented affinity rules, plus the one exception that the exact word INTEGER makes a primary key
        // the rowid alias — so columns testify in those terms, not in letters Sqlite never reads.
        command.CommandText = """
            SELECT type || ' | ' || name
            FROM sqlite_master
            WHERE type IN ('table', 'view', 'trigger', 'index') AND name NOT LIKE 'sqlite_%'
            UNION ALL
            SELECT 'column | ' || m.name || '.' || p.name || ' | ' ||
                   CASE
                       WHEN upper(coalesce(p.type, '')) = 'INTEGER' THEN 'INTEGER'
                       WHEN instr(upper(p.type), 'INT') > 0 THEN 'int-affinity'
                       WHEN instr(upper(p.type), 'CHAR') > 0 OR instr(upper(p.type), 'CLOB') > 0 OR instr(upper(p.type), 'TEXT') > 0 THEN 'text-affinity'
                       WHEN p.type IS NULL OR p.type = '' OR instr(upper(p.type), 'BLOB') > 0 THEN 'blob-affinity'
                       WHEN instr(upper(p.type), 'REAL') > 0 OR instr(upper(p.type), 'FLOA') > 0 OR instr(upper(p.type), 'DOUB') > 0 THEN 'real-affinity'
                       ELSE 'numeric-affinity'
                   END
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
}
