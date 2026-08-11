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
    /// The engine this database runs on, which is the axis a coverage report is grouped by.
    /// </summary>
    public EngineName Engine => engine.Name;

    /// <summary>
    /// Gets the PLUGIN and DATABASE statements a project needs to reach this database.
    /// </summary>
    public Nsql GetConfigurationNSql() => Nsql.From(
        $"""
         {plugin.Declaration("db")}

         DATABASE db (
           connection_string = '{ConnectionString.Replace("'", "''")}'
         );

         """);

    /// <summary>
    /// The <c>.editorconfig</c> a project needs alongside that configuration, or <see langword="null"/> when it
    /// needs none.
    /// </summary>
    /// <remarks>
    /// A plugin loaded from a path is reported on every run, and that report lands inside the captured plan.
    /// This override lets us hide it so snapshots stay consistent.
    /// </remarks>
    public string? GetEditorConfig() => plugin.Assembly is null
        ? null
        : """
          root = true

          [*]
          nschema_diagnostic.plugin-from-path.severity = none

          """.TrimStart();

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
    public abstract Task<IReadOnlyList<CatalogFact>> Catalog(CancellationToken cancellationToken = default);
}
