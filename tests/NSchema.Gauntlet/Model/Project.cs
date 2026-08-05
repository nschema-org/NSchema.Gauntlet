namespace NSchema.Gauntlet.Model;

/// <summary>
/// A throwaway NSchema project pointed at one case's database.
/// </summary>
public sealed class Project
{
    private const string SchemaFile = "schema.sql";

    private readonly string _schemaFile;
    private readonly string _databaseFile;
    private readonly string _stateFile;

    /// <summary>
    /// Creates a new project in the given directory.
    /// </summary>
    /// <param name="directory"></param>
    public Project(string directory)
    {
        Directory = directory;
        _databaseFile = Path.Combine(directory, "database.sql");
        _schemaFile = Path.Combine(directory, SchemaFile);
        _stateFile = Path.Combine(directory, "state.sql");

        File.WriteAllText(_stateFile, """
            STATE file (
              path = './nschema.state.json'
            );

            """.TrimStart());
    }

    /// <summary>
    /// Gets the directory where the project is located.
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// Connects the project to the given database.
    /// </summary>
    public void ConnectTo(Database database) => File.WriteAllText(_databaseFile, database.GetConfigurationNSql().Value);

    /// <summary>
    /// Gets this project's DDL.
    /// </summary>
    public Nsql GetSchema() => Nsql.From(string.Join("\n", Declarations()
        .OrderBy(file => file, StringComparer.Ordinal)
        .Select(File.ReadAllText)));

    /// <summary>
    /// Replaces the project's DDL.
    /// </summary>
    public void SetSchema(Nsql nsql)
    {
        ClearSchema();
        File.WriteAllText(_schemaFile, nsql.Value);
    }

    /// <summary>
    /// Clears the project's DDL.
    /// </summary>
    public void ClearSchema()
    {
        foreach (var file in Declarations().ToList())
        {
            File.Delete(file);
        }
    }

    // Every .sql file the project declares, which is all of them but its own configuration.
    private IEnumerable<string> Declarations() => System.IO.Directory
        .EnumerateFiles(Directory, "*.sql", SearchOption.AllDirectories)
        .Where(file => file != _databaseFile && file != _stateFile);
}
