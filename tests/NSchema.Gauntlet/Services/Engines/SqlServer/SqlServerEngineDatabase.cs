using Microsoft.Data.SqlClient;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Engines.SqlServer;

/// <inheritdoc />
public sealed class SqlServerEngineDatabase(string connectionString, PluginSettings plugin, string label) : EngineDatabase
{
    public override string ConfigurationSql =>
        $"""
         {plugin.Declaration(label)}

         DATABASE {label} (
           connection_string = '{connectionString.Replace("'", "''")}'
         );
         """;

    public override async Task Execute(string sql, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task<IReadOnlyList<string>> Inventory(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // The catalog's account of everything a user schema holds. A system-generated constraint name differs
        // between two databases holding the same schema, so those testify by shape rather than by name.
        command.CommandText = """
            SELECT kind + ' | ' + entry + CASE WHEN detail = '' THEN '' ELSE ' | ' + detail END
            FROM (
                SELECT 'table' AS kind, s.name + '.' + t.name AS entry, '' AS detail
                FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
              UNION ALL
                SELECT 'view', s.name + '.' + v.name, ''
                FROM sys.views v JOIN sys.schemas s ON s.schema_id = v.schema_id
              UNION ALL
                SELECT 'column', s.name + '.' + t.name + '.' + c.name,
                       CASE typ.name WHEN 'numeric' THEN 'decimal' ELSE typ.name END + ' null=' + CAST(c.is_nullable AS varchar(1)) + ' len=' + CAST(c.max_length AS varchar(10))
                FROM sys.columns c
                JOIN sys.tables t ON t.object_id = c.object_id
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                JOIN sys.types typ ON typ.user_type_id = c.user_type_id
              UNION ALL
                SELECT 'routine', s.name + '.' + o.name, o.type_desc COLLATE database_default
                FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id
                WHERE o.type IN ('FN', 'IF', 'TF', 'P', 'AF') AND o.is_ms_shipped = 0
              UNION ALL
                SELECT 'constraint',
                       s.name + '.' + t.name + '.' + CASE WHEN kc.is_system_named = 1 THEN '(system-named)' ELSE kc.name END,
                       kc.type_desc COLLATE database_default
                FROM sys.key_constraints kc JOIN sys.tables t ON t.object_id = kc.parent_object_id JOIN sys.schemas s ON s.schema_id = t.schema_id
              UNION ALL
                SELECT 'constraint',
                       s.name + '.' + t.name + '.' + CASE WHEN fk.is_system_named = 1 THEN '(system-named)' ELSE fk.name END,
                       'FOREIGN_KEY'
                FROM sys.foreign_keys fk JOIN sys.tables t ON t.object_id = fk.parent_object_id JOIN sys.schemas s ON s.schema_id = t.schema_id
              UNION ALL
                SELECT 'constraint',
                       s.name + '.' + t.name + '.' + CASE WHEN cc.is_system_named = 1 THEN '(system-named)' ELSE cc.name END,
                       'CHECK'
                FROM sys.check_constraints cc JOIN sys.tables t ON t.object_id = cc.parent_object_id JOIN sys.schemas s ON s.schema_id = t.schema_id
              UNION ALL
                SELECT 'trigger', s.name + '.' + t.name + '.' + tr.name, ''
                FROM sys.triggers tr JOIN sys.tables t ON t.object_id = tr.parent_id JOIN sys.schemas s ON s.schema_id = t.schema_id
              UNION ALL
                SELECT 'index', s.name + '.' + t.name + '.' + i.name, CAST(i.is_unique AS varchar(1))
                FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE i.name IS NOT NULL AND i.is_primary_key = 0 AND i.is_unique_constraint = 0
              UNION ALL
                SELECT 'sequence', s.name + '.' + sq.name, ''
                FROM sys.sequences sq JOIN sys.schemas s ON s.schema_id = sq.schema_id
            ) x
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

    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
