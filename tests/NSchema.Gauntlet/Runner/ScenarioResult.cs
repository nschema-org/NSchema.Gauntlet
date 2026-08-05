using System.Text;
using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Runner;

/// <summary>
/// The full account of one scenario run.
/// </summary>
public sealed record ScenarioResult
{
    /// <summary>
    /// The stages that ran, in order; what the snapshot pins.
    /// </summary>
    public required IReadOnlyList<ScenarioStage> Stages { get; init; }

    /// <summary>
    /// The stage the engine failed at, when it did.
    /// </summary>
    public ScenarioStage? Failure { get; init; }

    /// <summary>
    /// A plan taken afterward against state refreshed from the live database; empty means settled.
    /// </summary>
    public required CliResult Verification { get; init; }

    /// <summary>
    /// What the run amounted to, derived from where the refusal sits — so the result cannot disagree
    /// with its own evidence.
    /// </summary>
    /// <remarks>
    /// A refusal the engine reports and stops on is a documented capability, so it is a cell in the
    /// matrix rather than a failure. What makes it acceptable is <see cref="Converged"/> — refusing and
    /// then leaving the database half-migrated would not be.
    /// </remarks>
    public ScenarioOutcome Outcome => Failure switch
    {
        null => ScenarioOutcome.Succeeded,
        { Name: StageName.Bootstrap } => ScenarioOutcome.BootstrapFailed,
        _ => ScenarioOutcome.ChangeFailed,
    };

    /// <summary>
    /// The live database matches where the scenario was supposed to leave it — the target if it applied,
    /// the state it started from if it was refused.
    /// </summary>
    public bool Converged => Verification.ExitCode == 0;

    /// <summary>
    /// Renders the artifacts for the snapshot: the stages, exactly as the CLI reported them. The
    /// verdicts and the expectation are asserted by the test, not pinned here — a snapshot diff should
    /// always mean the SQL or the diagnostics changed.
    /// </summary>
    public string Render()
    {
        var report = new StringBuilder();

        foreach (var stage in Stages)
        {
            report.AppendLine($"=== {stage.Name.ToString().ToLowerInvariant()} ===");
            report.AppendLine(stage.Result.StandardOutput.Trim());

            if (stage.Result.StandardError.Trim() is { Length: > 0 } diagnostics)
            {
                report.AppendLine(diagnostics);
            }

            report.AppendLine($"exit: {stage.Result.ExitCode}");
            report.AppendLine();
        }

        return report.ToString();
    }
}
