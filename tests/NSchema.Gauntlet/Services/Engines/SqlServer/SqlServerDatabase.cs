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

    public override async Task<IReadOnlyList<string>> Catalog(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // The catalog's account of everything a user schema holds. A system-generated constraint name differs
        // between two databases holding the same schema, so those testify by shape rather than by name.
        // Indexes hang off sys.objects rather than sys.tables because a view carries them too, and an indexed
        // view's index must be the clustered one — so the kind is testified alongside uniqueness, and a view
        // testifies whether it is schema-bound. Losing any of that is what makes an indexed view stop being one.
        command.CommandText = """
            SELECT kind + ' | ' + entry + CASE WHEN detail = '' THEN '' ELSE ' | ' + detail END
            FROM (
                SELECT 'table' AS kind, s.name + '.' + t.name AS entry, '' AS detail
                FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
              UNION ALL
                SELECT 'view', s.name + '.' + v.name, 'schemabound=' + CAST(OBJECTPROPERTY(v.object_id, 'IsSchemaBound') AS varchar(1))
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
                       s.name + '.' + t.name + '.' + kc.name,
                       kc.type_desc COLLATE database_default + ' ' + i.type_desc COLLATE database_default
                FROM sys.key_constraints kc JOIN sys.tables t ON t.object_id = kc.parent_object_id JOIN sys.schemas s ON s.schema_id = t.schema_id
                JOIN sys.indexes i ON i.object_id = kc.parent_object_id AND i.index_id = kc.unique_index_id
              UNION ALL
                SELECT 'constraint',
                       s.name + '.' + t.name + '.' + fk.name,
                       'FOREIGN_KEY'
                FROM sys.foreign_keys fk JOIN sys.tables t ON t.object_id = fk.parent_object_id JOIN sys.schemas s ON s.schema_id = t.schema_id
              UNION ALL
                SELECT 'constraint',
                       s.name + '.' + t.name + '.' + cc.name,
                       'CHECK'
                FROM sys.check_constraints cc JOIN sys.tables t ON t.object_id = cc.parent_object_id JOIN sys.schemas s ON s.schema_id = t.schema_id
              UNION ALL
                SELECT 'trigger', s.name + '.' + t.name + '.' + tr.name, ''
                FROM sys.triggers tr JOIN sys.tables t ON t.object_id = tr.parent_id JOIN sys.schemas s ON s.schema_id = t.schema_id
              UNION ALL
                SELECT 'index', s.name + '.' + o.name + '.' + i.name,
                       'unique=' + CAST(i.is_unique AS varchar(1)) + ' ' + i.type_desc COLLATE database_default
                FROM sys.indexes i
                JOIN sys.objects o ON o.object_id = i.object_id
                JOIN sys.schemas s ON s.schema_id = o.schema_id
                WHERE i.name IS NOT NULL AND i.is_primary_key = 0 AND i.is_unique_constraint = 0
                  AND o.type IN ('U', 'V') AND o.is_ms_shipped = 0
              UNION ALL
                SELECT 'sequence', s.name + '.' + sq.name, ''
                FROM sys.sequences sq JOIN sys.schemas s ON s.schema_id = sq.schema_id
              UNION ALL
                -- How the range is cut, and which side the boundary falls on.
                SELECT 'partition function', pf.name COLLATE database_default,
                       pf.type_desc COLLATE database_default
                       + CASE pf.boundary_value_on_right WHEN 1 THEN ' RIGHT' ELSE ' LEFT' END
                       + ' fanout=' + CAST(pf.fanout AS varchar(10))
                FROM sys.partition_functions pf
              UNION ALL
                -- One row per boundary, so losing or moving a single one shows.
                SELECT 'partition boundary',
                       pf.name COLLATE database_default + '[' + CAST(rv.boundary_id AS varchar(10)) + ']',
                       CAST(rv.value AS nvarchar(200))
                FROM sys.partition_range_values rv
                JOIN sys.partition_functions pf ON pf.function_id = rv.function_id
              UNION ALL
                SELECT 'partition scheme', ps.name COLLATE database_default, pf.name COLLATE database_default
                FROM sys.partition_schemes ps
                JOIN sys.partition_functions pf ON pf.function_id = ps.function_id
              UNION ALL
                -- What each table and index actually sits on, and the column it is partitioned by. Every row
                -- above can survive intact while nothing uses it, so this is the one that tells a partitioned
                -- table from a plain one. Heaps carry no index name, hence the coalesce.
                SELECT 'storage', s.name + '.' + o.name + '.' + COALESCE(i.name COLLATE database_default, '(heap)'),
                       ds.type_desc COLLATE database_default + ' ' + ds.name COLLATE database_default
                       + COALESCE(' by ' + c.name COLLATE database_default, '')
                FROM sys.indexes i
                JOIN sys.objects o ON o.object_id = i.object_id
                JOIN sys.schemas s ON s.schema_id = o.schema_id
                JOIN sys.data_spaces ds ON ds.data_space_id = i.data_space_id
                LEFT JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.partition_ordinal > 0
                LEFT JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE o.type IN ('U', 'V') AND o.is_ms_shipped = 0
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
}
