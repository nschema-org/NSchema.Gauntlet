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

    public static GauntletProject Create(Engine engine, EngineDatabase database, IReadOnlyList<string>? packageSources = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "nschema-gauntlet", Path.GetRandomFileName());
        System.IO.Directory.CreateDirectory(directory);

        // The CLI restores plugins under the project, where NuGet's own configuration discovery applies —
        // so extra sources are declared the NuGet-native way: a NuGet.Config beside the project.
        if (packageSources is { Count: > 0 })
        {
            File.WriteAllText(
                Path.Combine(directory, "NuGet.Config"),
                $"""
                 <configuration>
                   <packageSources>
                 {string.Join("\n", packageSources.Select((source, i) => $"""    <add key="gauntlet-{i}" value="{source}" />"""))}
                   </packageSources>
                 </configuration>
                 """);
        }

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
        File.WriteAllText(Path.Combine(Directory, SchemaFile), Localize(nsql));

    /// <summary>
    /// Resolves the case's schema token for this engine, for SQL that runs outside the project.
    /// </summary>
    public string Localize(string sql) => sql.Replace(SchemaToken, _defaultSchema, StringComparison.Ordinal);

    /// <summary>
    /// Takes another project's declarations as this one's, leaving its configuration alone.
    /// </summary>
    /// <remarks>
    /// How a schema imported from one database is pointed at another: the declarations are the same project,
    /// the configuration is what makes it a different database.
    /// </remarks>
    public void TakeSchemaFrom(GauntletProject other)
    {
        ClearSchema();

        foreach (var source in Declarations(other.Directory))
        {
            var destination = Path.Combine(Directory, Path.GetRelativePath(other.Directory, source));
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
        }
    }

    /// <summary>
    /// Declares nothing at all — a project whose target is an empty database.
    /// </summary>
    public void ClearSchema()
    {
        foreach (var file in Declarations(Directory))
        {
            File.Delete(file);
        }
    }

    // Every .sql file the project declares, which is all of them but the configuration.
    private static IEnumerable<string> Declarations(string directory) =>
        System.IO.Directory
            .EnumerateFiles(directory, "*.sql", SearchOption.AllDirectories)
            .Where(file => !string.Equals(Path.GetFileName(file), ConfigurationFile, StringComparison.Ordinal));

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
