using NSchema.Gauntlet.Services.Engines;

namespace NSchema.Gauntlet.Services;

/// <summary>
/// Evicts the run's pinned package versions from every cache that would otherwise trust them.
/// </summary>
/// <remarks>
/// A nuget.org version is immutable, so a cache never lies about it. A local directory source is a build
/// output — the same version number can hold different bits an hour apart — so when the run declares one,
/// nothing pinned may be trusted cached: not NuGet's global packages, not the CLI's plugin store, and not
/// the gauntlet's own tool install.
/// </remarks>
public static class PackageCache
{
    public static void Clear(GauntletSettings settings, string cliInstallDirectory)
    {
        if (!settings.PackageSources.Any(Directory.Exists))
        {
            return;
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var pins = new[]
        {
            (settings.Cli.Package, settings.Cli.Version),
            (settings.Engines.Postgres.Plugin.Package, settings.Engines.Postgres.Plugin.Version),
            (settings.Engines.SqlServer.Plugin.Package, settings.Engines.SqlServer.Plugin.Version),
            (settings.Engines.Sqlite.Plugin.Package, settings.Engines.Sqlite.Plugin.Version),
        };

        foreach (var (package, version) in pins)
        {
            Delete(Path.Combine(profile, ".nuget", "packages", package.ToLowerInvariant(), version));
            Delete(Path.Combine(profile, ".nschema", "plugins", package, version));
        }

        Delete(cliInstallDirectory);
    }

    private static void Delete(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
