using Microsoft.Data.SqlClient;
using NSchema.Gauntlet.Model;
using Testcontainers.MsSql;

namespace NSchema.Gauntlet.Services.Engines.SqlServer;

/// <summary>
/// The SQL Server engine.
/// </summary>
/// <param name="settings"></param>
public sealed class SqlServerEngine(SqlServerSettings settings) : DatabaseEngine
{
    public static readonly EngineName Name = EngineName.From("sqlserver");

    private readonly MsSqlContainer _container = new MsSqlBuilder(settings.Image).Build();

    /// <remarks>
    /// Every SQL Server database has a <c>dbo</c>, and it is where an unqualified object lands.
    /// </remarks>
    protected override string DefaultSchema => "dbo";

    protected override async ValueTask Start(CancellationToken cancellationToken) => await _container.StartAsync(cancellationToken);

    protected override ValueTask Stop() => _container.DisposeAsync();

    protected override async ValueTask<Database> Provision(string caseName, CancellationToken cancellationToken)
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
        return new SqlServerDatabase(this, settings.Plugin, builder.ConnectionString);
    }

    // A case name may carry characters a bare identifier cannot; SQL Server allows 128.
    private static string Identifier(string name)
    {
        var sanitized = new string([.. name.Select(c => char.IsLetterOrDigit(c) ? c : '_')]);

        return sanitized.Length <= 128 ? sanitized : sanitized[..128];
    }
}
