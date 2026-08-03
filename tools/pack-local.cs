#!/usr/bin/env dotnet

// pack-local — build a package fresh and place it in the local feed, evicting every cache that might
// still vouch for previous bits published under the same version number.
//
// A nuget.org version is immutable; a local rebuild is not. Republishing 5.4.0 with different bits is
// exactly what this flow is for, so every step here assumes the version cannot be trusted anywhere:
//   - bin/obj Release are wiped first (an incremental build happily packs a stale assembly),
//   - the feed's existing package for the version is replaced,
//   - NuGet's global cache, the CLI's plugin store, and the gauntlet's tool install are all evicted.
// The gauntlet's own run evicts again (PackageCache) when packageSources names a local feed; this script
// covers everything that consumes the feed outside a gauntlet run.
//
// Usage:
//   tools/pack-local.cs <path-to.csproj> [<path-to.csproj> ...]
//
// The feed defaults to ~/.nschema/local-feed; override with NSCHEMA_LOCAL_FEED.

using System.Diagnostics;

var feed = Environment.GetEnvironmentVariable("NSCHEMA_LOCAL_FEED")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nschema", "local-feed");
Directory.CreateDirectory(feed);

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: pack-local.cs <path-to.csproj> [<path-to.csproj> ...]");
    return 2;
}

foreach (var project in args)
{
    if (!File.Exists(project))
    {
        Console.Error.WriteLine($"error: no such project: {project}");
        return 1;
    }

    var directory = Path.GetDirectoryName(Path.GetFullPath(project))!;
    var version = Evaluate(project, "Version");
    var id = Evaluate(project, "PackageId") is { Length: > 0 } packageId ? packageId : Path.GetFileNameWithoutExtension(project);

    Console.WriteLine($"── {id} {version}");

    // A fresh Release build: stale outputs are how a wrong assembly ends up inside a right-looking package.
    // The feed itself is a restore source, so a package can build against siblings that are also in flight.
    DeleteDirectory(Path.Combine(directory, "bin", "Release"));
    DeleteDirectory(Path.Combine(directory, "obj", "Release"));
    Run("build", project, "-c", "Release", "--nologo", "-v", "q", "--no-incremental", $"-p:RestoreAdditionalProjectSources={feed}");

    File.Delete(Path.Combine(feed, $"{id}.{version}.nupkg"));
    File.Delete(Path.Combine(feed, $"{id}.{version}.snupkg"));
    Run("pack", project, "-c", "Release", "--no-build", "--nologo", "-v", "q", "-o", feed);

    // Nothing may vouch for this version any more.
    var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    DeleteDirectory(Path.Combine(profile, ".nuget", "packages", id.ToLowerInvariant(), version));
    DeleteDirectory(Path.Combine(profile, ".nschema", "plugins", id, version));
    DeleteDirectory(Path.Combine(Path.GetTempPath(), "nschema-gauntlet", "cli", $"{id.ToLowerInvariant()}-{version}"));

    Console.WriteLine($"   → {Path.Combine(feed, $"{id}.{version}.nupkg")} (caches evicted)");
}

return 0;

static string Evaluate(string project, string property)
{
    var process = Process.Start(new ProcessStartInfo("dotnet")
    {
        ArgumentList = { "msbuild", project, $"-getProperty:{property}" },
        RedirectStandardOutput = true,
    })!;
    var value = process.StandardOutput.ReadToEnd().Trim();
    process.WaitForExit();
    return process.ExitCode == 0 ? value : throw new InvalidOperationException($"Could not evaluate {property} of '{project}'.");
}

static void Run(params string[] arguments)
{
    var startInfo = new ProcessStartInfo("dotnet");
    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    var process = Process.Start(startInfo)!;
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        Environment.Exit(process.ExitCode);
    }
}

static void DeleteDirectory(string path)
{
    if (Directory.Exists(path))
    {
        Directory.Delete(path, recursive: true);
    }
}
