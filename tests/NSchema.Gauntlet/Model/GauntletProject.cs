namespace NSchema.Gauntlet.Model;

/// <summary>
/// A throwaway NSchema project pointed at one case's database.
/// </summary>
/// <remarks>
/// The configuration is generated per run rather than checked in, because it carries the connection string
/// of a database that only exists for the length of one case.
/// </remarks>
public sealed class GauntletProject : IDisposable
{
    /// <summary>
    /// The token a case writes where its objects' schema goes, so one case runs on every engine.
    /// </summary>
    private const string SchemaToken = "{schema}";

    private const string SchemaFile = "schema.sql";
    private const string ConfigurationFile = "config.sql";

    private readonly string _defaultSchema;

    private GauntletProject(string directory, string defaultSchema)
    {
        Directory = directory;
        _defaultSchema = defaultSchema;
    }

    public string Directory { get; }

    public static GauntletProject Create(Engine engine, EngineDatabase database)
    {
        var directory = Path.Combine(Path.GetTempPath(), "nschema-gauntlet", Path.GetRandomFileName());
        System.IO.Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, ConfigurationFile),
            $"""
             {database.ConfigurationSql}

             STATE file (
               path = './nschema.state.json'
             );

             """);

        return new GauntletProject(directory, engine.DefaultSchema);
    }

    /// <summary>
    /// Replaces the project's declared schema. An empty schema is the teardown target.
    /// </summary>
    public void SetSchema(string nsql) =>
        File.WriteAllText(Path.Combine(Directory, SchemaFile), nsql.Replace(SchemaToken, _defaultSchema, StringComparison.Ordinal));

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Already gone.
        }
    }
}
