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

    public override async Task<IReadOnlyList<CatalogFact>> Catalog(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // Sqlite ignores a declared type's spelling — semantics are its documented affinity rules,
        // plus the one exception that the exact word INTEGER makes a primary key the rowid alias.
        //
        // Sqlite has no system catalogs to dump the way Postgres and SQL Server do: sqlite_master holds the
        // original DDL text and everything else comes from pragmas. So this is still a chosen projection,
        // and still carries the blind spot that implies — partial and expression indexes, WITHOUT ROWID,
        // STRICT and generated columns are all invisible here.
        command.CommandText = """
            WITH columns AS (
                SELECT m.name AS "table", p.name AS "column", p.type AS declared,
                       p."notnull" AS not_null, p.dflt_value AS default_value, p.pk AS pk
                FROM sqlite_master m JOIN pragma_table_info(m.name) p
                WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%'
            )
            SELECT 'sqlite_master', name, 'type', type
            FROM sqlite_master
            WHERE type IN ('table', 'view', 'trigger', 'index') AND name NOT LIKE 'sqlite_%'
            UNION ALL
            SELECT 'column', "table" || '.' || "column", 'affinity',
                   CASE
                       WHEN upper(coalesce(declared, '')) = 'INTEGER' THEN 'INTEGER'
                       WHEN instr(upper(declared), 'INT') > 0 THEN 'int-affinity'
                       WHEN instr(upper(declared), 'CHAR') > 0 OR instr(upper(declared), 'CLOB') > 0 OR instr(upper(declared), 'TEXT') > 0 THEN 'text-affinity'
                       WHEN declared IS NULL OR declared = '' OR instr(upper(declared), 'BLOB') > 0 THEN 'blob-affinity'
                       WHEN instr(upper(declared), 'REAL') > 0 OR instr(upper(declared), 'FLOA') > 0 OR instr(upper(declared), 'DOUB') > 0 THEN 'real-affinity'
                       ELSE 'numeric-affinity'
                   END
            FROM columns
            UNION ALL
            SELECT 'column', "table" || '.' || "column", 'notnull', CAST(not_null AS TEXT) FROM columns
            UNION ALL
            SELECT 'column', "table" || '.' || "column", 'default', coalesce(default_value, '') FROM columns
            UNION ALL
            SELECT 'column', "table" || '.' || "column", 'pk', CAST(pk AS TEXT) FROM columns
            """;

        var facts = new List<CatalogFact>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            facts.Add(new CatalogFact(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return facts;
    }
}
