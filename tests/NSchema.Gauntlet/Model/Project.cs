namespace NSchema.Gauntlet.Model;

/// <summary>
/// A throwaway NSchema project pointed at one case's database.
/// </summary>
public sealed class Project
{
    private const string SchemaFile = "schema.sql";

    private readonly string _schemaFile;
    private readonly string _databaseFile;

    /// <summary>
    /// Creates a new project in the given directory.
    /// </summary>
    /// <param name="directory"></param>
    public Project(string directory)
    {
        Directory = directory;
        _databaseFile = Path.Combine(directory, "database.sql");
        _schemaFile = Path.Combine(directory, SchemaFile);

        var stateFile = Path.Combine(directory, "state.sql");
        File.WriteAllText(stateFile, "STATE file (path = './nschema.state.json');");
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
    public Nsql GetSchema() => Nsql.From(File.ReadAllText(_schemaFile));

    /// <summary>
    /// Replaces the project's DDL.
    /// </summary>
    public void SetSchema(Nsql nsql) => File.WriteAllText(_schemaFile, nsql.Value);

    /// <summary>
    /// Clear's the project's DDL.
    /// </summary>
    public void ClearSchema() => File.Delete(_schemaFile);
}
