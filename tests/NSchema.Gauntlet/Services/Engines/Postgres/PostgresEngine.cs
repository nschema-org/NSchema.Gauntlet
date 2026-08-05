using Npgsql;
using NSchema.Gauntlet.Model;
using Testcontainers.PostgreSql;

namespace NSchema.Gauntlet.Services.Engines.Postgres;

/// <summary>
/// The Postgres engine.
/// </summary>
public sealed class PostgresEngine(PostgresSettings settings) : DatabaseEngine
{
    public static readonly EngineName Name = EngineName.From("postgres");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(settings.Image).Build();

    protected override string DefaultSchema => "public";

    protected override async ValueTask Start(CancellationToken cancellationToken) => await _container.StartAsync(cancellationToken);

    protected override ValueTask Stop() => _container.DisposeAsync();

    protected override async ValueTask<Database> Provision(string name, CancellationToken cancellationToken)
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
        return new PostgresDatabase(this, settings.Plugin, builder.ConnectionString);
    }

    // Postgres truncates identifiers at 63 bytes, and a case name may carry characters a bare identifier cannot.
    private static string Identifier(string name)
    {
        var sanitized = new string([.. name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_')]);

        return sanitized.Length <= 63 ? sanitized : sanitized[..63];
    }
}
