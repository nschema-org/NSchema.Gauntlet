using Microsoft.Data.SqlClient;
using NSchema.Gauntlet.Model;
using Testcontainers.MsSql;

namespace NSchema.Gauntlet.Services.Engines.SqlServer;

/// <summary>
/// SQL Server, backed by one container serving a database per case.
/// </summary>
public sealed class SqlServerEngine(SqlServerSettings settings) : Engine
{
    internal const string Name = "sqlserver";

    private readonly MsSqlContainer _container = new MsSqlBuilder(settings.Image).Build();

    /// <remarks>
    /// Every SQL Server database has a <c>dbo</c>, and it is where an unqualified object lands.
    /// </remarks>
    public override string DefaultSchema => "dbo";

    protected override Task Start(CancellationToken cancellationToken) => _container.StartAsync(cancellationToken);

    protected override ValueTask Stop() => _container.DisposeAsync();

    protected override async Task<EngineDatabase> Provision(string caseName, CancellationToken cancellationToken)
    {
        var admin = _container.GetConnectionString();
        var database = Identifier(caseName);

        await using (var connection = new SqlConnection(admin))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{database}];";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var builder = new SqlConnectionStringBuilder(admin) { InitialCatalog = database };

        return new SqlServerEngineDatabase(builder.ConnectionString, settings.Plugin, Name);
    }

    // A case name may carry characters a bare identifier cannot; SQL Server allows 128.
    private static string Identifier(string name)
    {
        var sanitized = new string([.. name.Select(c => char.IsLetterOrDigit(c) ? c : '_')]);

        return sanitized.Length <= 128 ? sanitized : sanitized[..128];
    }
}
