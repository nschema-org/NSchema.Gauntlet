using Microsoft.Data.Sqlite;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Sqlite;

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
