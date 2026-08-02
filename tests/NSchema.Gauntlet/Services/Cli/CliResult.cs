namespace NSchema.Gauntlet.Services.Cli;

/// <summary>
/// The outcome of one CLI invocation.
/// </summary>
/// <remarks>
/// Exit codes follow the CLI's contract: 0 no changes, 1 error, 2 changes pending.
/// </remarks>
public sealed record CliResult(string Command, int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode is 0 or 2;

    public string Describe() => $"`nschema {Command}` exited {ExitCode}{Environment.NewLine}{StandardOutput}{StandardError}";
}
