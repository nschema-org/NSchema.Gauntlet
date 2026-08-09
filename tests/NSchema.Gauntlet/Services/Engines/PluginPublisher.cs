using System.Text;
using CliWrap;

namespace NSchema.Gauntlet.Services.Engines;

/// <summary>
/// Builds a provider project into NSchema's shared plugin cache, so a run can exercise a provider that has not been
/// released.
/// </summary>
/// <remarks>
/// <para>
/// The cache is a directory convention: NSchema loads
/// <c>~/.nschema/plugins/{package}/{version}/publish/nschema-plugin-host.dll</c> and resolves that assembly's
/// dependencies from the <c>.deps.json</c> beside it. Its own restore produces that by synthesising a tiny project
/// which package-references the plugin and publishing it. This does the same thing with a project reference, which
/// is the only difference between a released plugin and one that exists on disk.
/// </para>
/// <para>
/// Writing straight into the cache rather than declaring a path is what keeps the plan snapshots comparable — see
/// <see cref="PluginSettings"/> for why that matters.
/// </para>
/// </remarks>
public sealed class PluginPublisher(string tempDirectory)
{
    /// <summary>
    /// The assembly NSchema's loader hands to its resolver, which is what the published project has to be called.
    /// </summary>
    private const string HostAssembly = "nschema-plugin-host";

    private static readonly string _cacheRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nschema", "plugins");

    /// <summary>
    /// Builds <paramref name="project"/> into the cache entry the given package and version name.
    /// </summary>
    public async Task Publish(string project, string package, string version, CancellationToken cancellationToken)
    {
        var publishDirectory = Path.Combine(_cacheRoot, package, version, "publish");

        // A released version is immutable, so NSchema's own restore may skip a version it already holds. This one is
        // rebuilt every run and must never be served from what a previous run left, so the entry goes first.
        if (Directory.Exists(publishDirectory))
        {
            Directory.Delete(publishDirectory, recursive: true);
        }

        var scratch = Path.Combine(tempDirectory, "plugin-host", package);
        Directory.CreateDirectory(scratch);
        await File.WriteAllTextAsync(Path.Combine(scratch, $"{HostAssembly}.csproj"), Host(project), cancellationToken);

        var output = new StringBuilder();

        // Qualified: this assembly has its own Cli namespace, which shadows CliWrap's entry point.
        var result = await CliWrap.Cli.Wrap("dotnet")
            .WithArguments(["publish", Path.Combine(scratch, $"{HostAssembly}.csproj"), "-c", "Debug", "--nologo", "-v", "q", "-o", publishDirectory])
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(output))
            .ExecuteAsync(cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Could not build '{project}' into the plugin cache:{Environment.NewLine}{output}");
        }
    }

    // The same project NSchema synthesises for a restore, with a project reference where it would put a package
    // reference. EnableDynamicLoading is what writes the .deps.json and the closure the resolver reads.
    private static string Host(string project) =>
        $"""
         <Project Sdk="Microsoft.NET.Sdk">
           <PropertyGroup>
             <TargetFramework>net10.0</TargetFramework>
             <EnableDynamicLoading>true</EnableDynamicLoading>
             <IsPackable>false</IsPackable>
           </PropertyGroup>
           <ItemGroup>
             <ProjectReference Include="{Path.GetFullPath(project)}" />
           </ItemGroup>
         </Project>
         """;
}
