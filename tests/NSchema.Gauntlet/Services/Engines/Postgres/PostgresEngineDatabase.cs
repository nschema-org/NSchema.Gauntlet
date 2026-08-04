using Npgsql;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Engines.Postgres;

/// <inheritdoc />
public sealed class PostgresEngineDatabase(string connectionString, PluginSettings plugin, string label) : EngineDatabase
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
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task<IReadOnlyList<string>> Inventory(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // The catalog's account of everything a user schema holds — deliberately broader than NSchema's
        // model (all routine kinds, for one), because what NSchema cannot see is what this exists to catch.
        command.CommandText = """
            SELECT kind || ' | ' || identity || CASE WHEN detail = '' THEN '' ELSE ' | ' || detail END
            FROM (
                SELECT 'table' AS kind, n.nspname || '.' || c.relname AS identity, '' AS detail
                FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE c.relkind IN ('r', 'p') AND n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'
              UNION ALL
                SELECT 'view', n.nspname || '.' || c.relname, CASE c.relkind WHEN 'm' THEN 'materialized' ELSE '' END
                FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE c.relkind IN ('v', 'm') AND n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'
              UNION ALL
                SELECT 'column', c.table_schema || '.' || c.table_name || '.' || c.column_name,
                       coalesce(c.data_type, '') || ' null=' || c.is_nullable || ' default=' || coalesce(c.column_default, '')
                FROM information_schema.columns c
                WHERE c.table_schema !~ '^pg_' AND c.table_schema <> 'information_schema'
              UNION ALL
                SELECT 'routine', n.nspname || '.' || p.proname || '(' || pg_get_function_identity_arguments(p.oid) || ')', p.prokind::text
                FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'
              UNION ALL
                SELECT 'constraint', n.nspname || '.' || cl.relname || '.' || con.conname, con.contype::text
                FROM pg_constraint con JOIN pg_class cl ON cl.oid = con.conrelid JOIN pg_namespace n ON n.oid = cl.relnamespace
                WHERE n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'
              UNION ALL
                SELECT 'index', schemaname || '.' || indexname, ''
                FROM pg_indexes
                WHERE schemaname !~ '^pg_' AND schemaname <> 'information_schema'
              UNION ALL
                SELECT DISTINCT 'trigger', t.event_object_schema || '.' || t.event_object_table || '.' || t.trigger_name, ''
                FROM information_schema.triggers t
                WHERE t.event_object_schema !~ '^pg_' AND t.event_object_schema <> 'information_schema'
              UNION ALL
                SELECT 'type', n.nspname || '.' || t.typname, t.typtype::text
                FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                WHERE n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'
                  AND (t.typtype IN ('e', 'd')
                       OR (t.typtype = 'c' AND EXISTS (SELECT 1 FROM pg_class rc WHERE rc.oid = t.typrelid AND rc.relkind = 'c')))
              UNION ALL
                SELECT 'sequence', sequence_schema || '.' || sequence_name, ''
                FROM information_schema.sequences
                WHERE sequence_schema !~ '^pg_' AND sequence_schema <> 'information_schema'
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
