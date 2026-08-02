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

    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
