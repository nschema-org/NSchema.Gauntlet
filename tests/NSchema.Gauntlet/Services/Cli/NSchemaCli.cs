using System.Text;
using CliWrap;

namespace NSchema.Gauntlet.Services.Cli;

/// <summary>
/// Drives the installed NSchema CLI against a project directory.
/// </summary>
/// <remarks>
/// The gauntlet runs through the CLI rather than the engine API because that is the surface users have,
/// and it exercises configuration, plugin loading and state along the way.
/// </remarks>
public sealed class NSchemaCli(string projectDirectory)
{
    private const string Executable = "nschema";

    public async Task<CliResult> Run(CancellationToken cancellationToken, params string[] arguments)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();

        string[] full = ["--directory", projectDirectory, "--no-color", .. arguments];

        // Qualified: this namespace is itself called Cli, which shadows CliWrap's entry point.
        var result = await CliWrap.Cli.Wrap(Executable)
            .WithArguments(full)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
            .ExecuteAsync(cancellationToken);

        return new CliResult(string.Join(' ', arguments), result.ExitCode, output.ToString(), error.ToString());
    }
}
