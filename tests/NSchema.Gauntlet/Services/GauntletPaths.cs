namespace NSchema.Gauntlet.Services;

/// <summary>
/// Where the case directories live.
/// </summary>
public static class GauntletPaths
{
    private static readonly string _root = RepositoryRoot();

    /// <summary>
    /// Acquired schemas, in their own engine's DDL.
    /// </summary>
    public static string Corpus => Path.Combine(_root, "corpus");

    /// <summary>
    /// Authored before/after pairs, in NSQL.
    /// </summary>
    public static string Scenarios => Path.Combine(_root, "scenarios");

    private static string RepositoryRoot()
    {
        const string rootMarker = ".git";
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, rootMarker)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate {rootMarker} above {AppContext.BaseDirectory}.");
    }
}
