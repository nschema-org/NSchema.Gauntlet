using System.Text;
using NSchema.Gauntlet.Services.Engines;

namespace NSchema.Gauntlet.Model;

/// <summary>
/// A real database.
/// </summary>
public abstract class Database(DatabaseEngine engine, PluginSettings plugin, string connectionString)
{
    /// <summary>
    /// Gets the connection string used to connect to this database.
    /// </summary>
    protected string ConnectionString { get; } = connectionString;

    /// <summary>
    /// Gets the PLUGIN and DATABASE statements a project needs to reach this database.
    /// </summary>
    public Nsql GetConfigurationNSql() => Nsql.From(
        $"""
         PLUGIN db (
           source = '{plugin.Package}',
           version = '{plugin.Version}'
         );

         DATABASE db (
           connection_string = '{ConnectionString.Replace("'", "''")}'
         );

         """);

    /// <summary>
    /// Localizes NSQL for this database's engine.
    /// </summary>
    public Nsql Localize(Nsql nsql) => engine.Localize(nsql);

    /// <summary>
    /// Localizes and runs SQL directly against the database.
    /// </summary>
    public Task Execute(Sql sql, CancellationToken cancellationToken = default)
    {
        sql = engine.Localize(sql);
        return ExecuteCore(sql, cancellationToken);
    }

    /// <summary>
    /// Runs SQL directly against the database.
    /// </summary>
    protected abstract Task ExecuteCore(Sql sql, CancellationToken cancellationToken = default);

    /// <summary>
    /// The engine's own account of this database's schema, one ordered row per fact, read straight from its catalog.
    /// Two databases holding the same schema testify identically; NSchema is nowhere in the loop.
    /// </summary>
    public abstract Task<IReadOnlyList<string>> Catalog(CancellationToken cancellationToken = default);
}
