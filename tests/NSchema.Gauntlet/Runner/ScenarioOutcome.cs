using System.Text;
using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Runner;

/// <summary>
/// What happened when a scenario ran.
/// </summary>
/// <remarks>
/// Establishing the before state is setup, not assertion: a failure there is reported as
/// <see cref="SetupFailure"/> so a broken before state never reads as a failure of the change under test.
/// </remarks>
public sealed record ScenarioOutcome
{
    public CliResult? SetupFailure { get; init; }

    public CliResult? Plan { get; init; }

    public CliResult? Apply { get; init; }

    /// <summary>
    /// A plan taken after applying, against state refreshed from the live database.
    /// </summary>
    public CliResult? Verification { get; init; }

    /// <summary>
    /// The engine refused the change rather than attempting it.
    /// </summary>
    /// <remarks>
    /// A limitation the engine reports and stops on is a documented capability, so it is a cell in the
    /// matrix rather than a failure. What makes it acceptable is <see cref="Converged"/> — refusing and
    /// then leaving the database half-migrated would not be.
    /// </remarks>
    public bool Blocked => Apply?.Succeeded == false;

    /// <summary>
    /// The live database matches where the scenario was supposed to leave it — the target if it applied,
    /// the state it started from if it was blocked.
    /// </summary>
    public bool Converged => Verification?.ExitCode == 0;

    public static ScenarioOutcome Failed(CliResult setup) => new() { SetupFailure = setup };

    /// <summary>
    /// The snapshot surface: the plan, what applying it did, and whether the database agreed afterwards.
    /// </summary>
    /// <remarks>
    /// Diagnostics are reported on standard error, and an engine limitation reaches the snapshot as a
    /// diagnostic — so leaving them out would silently drop the very thing the matrix records.
    /// </remarks>
    public string Report()
    {
        var report = new StringBuilder();

        Append(report, "plan", Plan);
        Append(report, "apply", Apply);
        report.AppendLine($"blocked: {Blocked}");
        report.AppendLine($"converged: {Converged}");

        return report.ToString();
    }

    private static void Append(StringBuilder report, string stage, CliResult? result)
    {
        report.AppendLine($"=== {stage} ===");
        report.AppendLine(result?.StandardOutput.Trim());

        if (result?.StandardError.Trim() is { Length: > 0 } diagnostics)
        {
            report.AppendLine(diagnostics);
        }

        report.AppendLine($"exit: {result?.ExitCode}");
        report.AppendLine();
    }
}
