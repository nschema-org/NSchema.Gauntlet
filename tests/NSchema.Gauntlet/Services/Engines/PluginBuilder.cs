using System.Text;
using CliWrap;

namespace NSchema.Gauntlet.Services.Engines;

/// <summary>
/// Builds a provider project, so a run can exercise a provider that has not been released.
/// </summary>
/// <remarks>
/// <para>
/// The build output is loaded directly: a provider already writes the <c>.deps.json</c> its dependencies resolve
/// from, which is the only thing NSchema's loader needs beyond the assembly itself. Nothing is copied into the
/// shared plugin cache, so a run cannot be served what a previous one left there.
/// </para>
/// <para>
/// Building is this class's whole job because nothing else does it: the run consumes whatever sits at the end of
/// the path, so an unbuilt provider would silently be an old one.
/// </para>
/// </remarks>
public sealed class PluginBuilder
{
    private const string Configuration = "Debug";

    /// <summary>
    /// Builds <paramref name="project"/> and returns the assembly it produced.
    /// </summary>
    public async Task<string> Build(string project, CancellationToken cancellationToken)
    {
        await Run(project, ["build", project, "-c", Configuration, "--nologo", "-v", "q"], cancellationToken);

        // Asked for rather than assembled from the project's directory, which would hard-code a target framework
        // and a configuration that only this file believes in. -getProperty evaluates without building, so it is
        // a question about the build that just ran rather than a second one.
        var target = await Run(
            project,
            ["build", project, "-c", Configuration, "--nologo", "-v", "q", "-getProperty:TargetPath"],
            cancellationToken);

        return target.Trim();
    }

    private static async Task<string> Run(string project, string[] arguments, CancellationToken cancellationToken)
    {
        var output = new StringBuilder();

        // Qualified: this assembly has its own Cli namespace, which shadows CliWrap's entry point.
        var result = await CliWrap.Cli.Wrap("dotnet")
            .WithArguments(arguments)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(output))
            .ExecuteAsync(cancellationToken);

        return result.ExitCode == 0
            ? output.ToString()
            : throw new InvalidOperationException($"Could not build '{project}':{Environment.NewLine}{output}");
    }
}
