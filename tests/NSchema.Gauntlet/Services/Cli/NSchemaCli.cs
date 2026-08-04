using System.Text;
using CliWrap;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Cli;

/// <summary>
/// Drives the run's NSchema CLI against a project directory.
/// </summary>
/// <remarks>
/// The gauntlet runs through the CLI rather than the engine API because that is the surface users have,
/// and it exercises configuration, plugin loading and state along the way.
/// </remarks>
public sealed class NSchemaCli(CliInstallation cli, string projectDirectory)
{
    public Task<CliResult> Init(CancellationToken cancellationToken) => RunCore(["init"], cancellationToken);

    public Task<CliResult> Refresh(CancellationToken cancellationToken) => RunCore(["refresh"], cancellationToken);

    public Task<CliResult> Import(CancellationToken cancellationToken) =>
        RunCore(["import", "--out-dir", projectDirectory, "--force"], cancellationToken);

    public Task<CliResult> Format(CancellationToken cancellationToken) => RunCore(["format", "--check"], cancellationToken);

    public Task<CliResult> Plan(DestructiveActionPolicy destructiveActions, bool detailedExitCode, CancellationToken cancellationToken)
    {
        List<string> args = ["plan", "--destructive-actions", destructiveActions.ToString()];

        if (detailedExitCode)
        {
            args.Add("--detailed-exitcode");
        }

        return RunCore(args, cancellationToken);
    }

    public Task<CliResult> Apply(DestructiveActionPolicy destructiveActions, CancellationToken cancellationToken) =>
        RunCore(["apply", "--auto-approve", "--destructive-actions", destructiveActions.ToString()], cancellationToken);

    private async Task<CliResult> RunCore(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();

        List<string> full = [
            "--directory", projectDirectory,
            "--no-color",
            .. arguments
        ];

        // Qualified: this namespace is itself called Cli, which shadows CliWrap's entry point.
        var result = await CliWrap.Cli.Wrap(cli.Executable)
            .WithArguments(full)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
            .ExecuteAsync(cancellationToken);

        return new CliResult(string.Join(' ', full), result.ExitCode, output.ToString(), error.ToString());
    }
}
