using System.Text;
using CliWrap;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Cli;

/// <summary>
/// An NSchema CLI client.
/// </summary>
public sealed class NSchemaClient
{
    private readonly CliSettings _settings;
    private readonly NuGetSettings _nuget;

    private readonly string _directory;
    private readonly string _executable;
    private readonly SemaphoreSlim _installGate = new(1, 1);

    /// <summary>
    /// The pinned NSchema CLI, acquired for the run.
    /// </summary>
    public NSchemaClient(CliSettings settings, NuGetSettings nuget, string tempDirectory)
    {
        _settings = settings;
        _nuget = nuget;
        _directory = Path.Combine(tempDirectory, "cli");
        _executable = Path.Combine(_directory, OperatingSystem.IsWindows() ? "nschema.exe" : "nschema");
    }

    public Task<ErrorOr<Success>> Init(string directory, CancellationToken ct) => Require(directory, ["init"], ct);
    public Task<ErrorOr<Success>> Refresh(string directory, CancellationToken ct) => Require(directory, ["refresh"], ct);
    public Task<ErrorOr<Success>> Import(string directory, CancellationToken ct) => Require(directory, ["import", "--force"], ct);
    public Task<CliResult> Format(string directory, CancellationToken ct) => Run(directory, ["format", "--check"], ct);

    public Task<CliResult> Plan(string dir, DestructiveActionPolicy destructiveActions, bool detailedExitCode, CancellationToken ct)
    {
        List<string> args = ["plan", "--destructive-actions", destructiveActions.ToString()];

        if (detailedExitCode)
        {
            args.Add("--detailed-exitcode");
        }
        return Run(dir, args, ct);
    }

    public Task<CliResult> Apply(string directory, DestructiveActionPolicy destructiveActions, CancellationToken ct) => Run(directory, ["apply", "--auto-approve", "--destructive-actions", destructiveActions.ToString()], ct);

    private async Task<ErrorOr<Success>> Require(string directory, List<string> arguments, CancellationToken ct)
    {
        var result = await Run(directory, arguments, ct);
        return result.Succeeded ? Result.Success : Error.Failure(arguments[0], result.Describe());
    }

    private async Task<CliResult> Run(string directory, IEnumerable<string> arguments, CancellationToken ct)
    {
        await EnsureInstalled(ct);
        var output = new StringBuilder();
        var error = new StringBuilder();

        List<string> full = ["--no-color", .. arguments];

        var result = await CliWrap.Cli.Wrap(_executable)
            .WithArguments(full)
            .WithWorkingDirectory(directory)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
            .ExecuteAsync(ct);

        return new CliResult(string.Join(' ', full), result.ExitCode, output.ToString(), error.ToString());
    }

    private async ValueTask EnsureInstalled(CancellationToken ct)
    {
        if (File.Exists(_executable))
        {
            return;
        }

        // Parallel cases share one client; only the first may install.
        await _installGate.WaitAsync(ct);

        try
        {
            if (File.Exists(_executable))
            {
                return;
            }

            var output = new StringBuilder();

            // Qualified: this namespace is itself called Cli, which shadows CliWrap's entry point.
            var result = await CliWrap.Cli.Wrap("dotnet")
                .WithArguments([
                    "tool", "install", _settings.Package,
                    "--version", _settings.Version,
                    "--tool-path", _directory,
                    .. _nuget.Sources.SelectMany(source => new[] { "--add-source", source })
                ])
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(output))
                .ExecuteAsync(ct);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Could not install {_settings.Package}@{_settings.Version}:\n{output}");
            }
        }
        finally
        {
            _installGate.Release();
        }
    }
}
