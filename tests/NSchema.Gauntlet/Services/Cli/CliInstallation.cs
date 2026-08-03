using System.Text;
using CliWrap;

namespace NSchema.Gauntlet.Services.Cli;

/// <summary>
/// The pinned NSchema CLI, acquired for the run.
/// </summary>
/// <remarks>
/// The tool installs into a version-keyed path under the temp directory, so a version an earlier run
/// already acquired is reused and the install is a no-op — including offline.
/// </remarks>
public sealed class CliInstallation(CliSettings settings, IReadOnlyList<string> packageSources)
{
    /// <summary>
    /// Where this version installs, for the mutable-pin eviction.
    /// </summary>
    public string Directory { get; } = Path.Combine(Path.GetTempPath(), "nschema-gauntlet", "cli", $"{settings.Package.ToLowerInvariant()}-{settings.Version}");

    /// <summary>
    /// The installed executable.
    /// </summary>
    public string Executable => Path.Combine(Directory, OperatingSystem.IsWindows() ? "nschema.exe" : "nschema");

    /// <summary>
    /// Installs the pinned version if this machine does not already have it.
    /// </summary>
    public async ValueTask Install(CancellationToken cancellationToken)
    {
        if (File.Exists(Executable))
        {
            return;
        }

        var output = new StringBuilder();

        // Qualified: this namespace is itself called Cli, which shadows CliWrap's entry point.
        var result = await CliWrap.Cli.Wrap("dotnet")
            .WithArguments(["tool", "install", settings.Package, "--version", settings.Version, "--tool-path", Directory,
                .. packageSources.SelectMany(source => new[] { "--add-source", source })])
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(output))
            .ExecuteAsync(cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not install {settings.Package} {settings.Version}:{Environment.NewLine}{output}");
        }
    }
}
