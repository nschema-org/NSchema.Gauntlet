using Npgsql;
using NSchema.Gauntlet.Model;
using Testcontainers.PostgreSql;

namespace NSchema.Gauntlet.Services.Postgres;

/// <summary>
/// Postgres, backed by one container serving a database per case.
/// </summary>
public sealed class PostgresEngine(string name, EngineSettings settings) : Engine(name)
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(settings.RequiredImage(name)).Build();

    public override string DefaultSchema => "public";

    protected override Task Start(CancellationToken cancellationToken) => _container.StartAsync(cancellationToken);

    protected override ValueTask Stop() => _container.DisposeAsync();

    protected override async Task<EngineDatabase> Provision(string name, CancellationToken cancellationToken)
    {
        var admin = _container.GetConnectionString();
        var database = Identifier(name);

        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{database}\";";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var builder = new NpgsqlConnectionStringBuilder(admin) { Database = database };

        return new PostgresEngineDatabase(builder.ConnectionString, settings.Plugin, Name);
    }

    // Postgres truncates identifiers at 63 bytes, and a case name may carry characters a bare identifier cannot.
    private static string Identifier(string name)
    {
        var sanitized = new string([.. name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_')]);

        return sanitized.Length <= 63 ? sanitized : sanitized[..63];
    }
}
