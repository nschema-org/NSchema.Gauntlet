using System.Text;
using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Runner;

/// <summary>
/// What happened when a corpus case was round-tripped.
/// </summary>
public sealed record CorpusOutcome
{
    public CliResult? SetupFailure { get; init; }

    /// <summary>
    /// The project NSchema wrote from the live database.
    /// </summary>
    public CliResult? Import { get; init; }

    /// <summary>
    /// Formatting the imported project, which should have nothing to change.
    /// </summary>
    public CliResult? Format { get; init; }

    /// <summary>
    /// The first plan of the imported project against the database it came from. A schema NSchema did not
    /// create is unmanaged, so this legitimately adopts — what it must not do is add, change or destroy.
    /// </summary>
    public CliResult? Adoption { get; init; }

    /// <summary>
    /// A second plan, once the adoption has been applied.
    /// </summary>
    public CliResult? Verification { get; init; }

    /// <summary>
    /// Applying the same declarations to an empty database.
    /// </summary>
    public CliResult? Rebuild { get; init; }

    /// <summary>
    /// A plan against the rebuilt database, which should want nothing.
    /// </summary>
    public CliResult? RebuildVerification { get; init; }

    /// <summary>
    /// Applying a project that declares nothing, which drops what was built.
    /// </summary>
    public CliResult? Teardown { get; init; }

    /// <summary>
    /// A plan against the emptied database, which should want nothing.
    /// </summary>
    public CliResult? TeardownVerification { get; init; }

    /// <summary>
    /// The writer emits what the formatter would have written.
    /// </summary>
    public bool Canonical => Format?.ExitCode == 0;

    /// <summary>
    /// Nothing at all is left to do: NSchema agrees with its own description of the database.
    /// </summary>
    public bool RoundTrips => Verification?.ExitCode == 0;

    /// <summary>
    /// The SQL NSchema renders builds the schema it described.
    /// </summary>
    public bool Rebuilds => RebuildVerification?.ExitCode == 0;

    /// <summary>
    /// It comes apart again, in an order the engine accepts.
    /// </summary>
    public bool TearsDown => TeardownVerification?.ExitCode == 0;

    public static CorpusOutcome Failed(CliResult setup) => new() { SetupFailure = setup };

    /// <summary>
    /// The snapshot surface. The imported project itself is not included.
    /// </summary>
    /// <remarks>
    /// The first plan is the one worth recording: both its sides come from the same introspection, one of them
    /// by way of the writer and the parser, so it is the round trip made visible. The second plan is empty by
    /// construction — its whole content is <see cref="RoundTrips"/>, until it isn't, and then what it still
    /// wants to do is the finding.
    /// </remarks>
    public string Report()
    {
        var report = new StringBuilder();

        report.AppendLine($"canonical: {Canonical}");
        report.AppendLine($"round-trips: {RoundTrips}");
        report.AppendLine($"rebuilds: {Rebuilds}");
        report.AppendLine($"tears down: {TearsDown}");
        report.AppendLine();
        report.AppendLine("=== first plan against the imported project ===");
        report.AppendLine(Adoption?.StandardOutput.Trim());

        if (Adoption?.StandardError.Trim() is { Length: > 0 } diagnostics)
        {
            report.AppendLine(diagnostics);
        }

        Unfinished(report, "left over after adopting", RoundTrips, Verification);
        Unfinished(report, "left over after rebuilding", Rebuilds, RebuildVerification);
        Unfinished(report, "left over after tearing down", TearsDown, TeardownVerification);

        return report.ToString();
    }

    // A leg that settled has nothing to say; one that did not is a finding, and what it still wants to do is it.
    private static void Unfinished(StringBuilder report, string stage, bool settled, CliResult? result)
    {
        if (settled)
        {
            return;
        }

        report.AppendLine();
        report.AppendLine($"=== {stage} ===");
        report.AppendLine(result?.StandardOutput.Trim());
        report.AppendLine(result?.StandardError.Trim());
    }
}
