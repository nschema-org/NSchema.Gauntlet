using System.Text;
using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Runner;

/// <summary>
/// The full account of one corpus round trip.
/// </summary>
public sealed record CorpusResult
{
    /// <summary>
    /// Formatting the imported project, which should have nothing to change.
    /// </summary>
    public required CliResult Format { get; init; }

    /// <summary>
    /// The first plan of the imported project against the database it came from. A schema NSchema did not
    /// create is unmanaged, so this legitimately adopts — what it must not do is add, change or destroy.
    /// </summary>
    public required CliResult Adoption { get; init; }

    /// <summary>
    /// A second plan, once the adoption has been applied.
    /// </summary>
    public required CliResult Verification { get; init; }

    /// <summary>
    /// Applying the same declarations to an empty database.
    /// </summary>
    public required CliResult Rebuild { get; init; }

    /// <summary>
    /// A plan against the rebuilt database, which should want nothing.
    /// </summary>
    public required CliResult RebuildVerification { get; init; }

    /// <summary>
    /// The engines' own accounts of source and rebuild, compared; null when the rebuild did not complete.
    /// </summary>
    public IReadOnlyList<string>? EngineTestimony { get; init; }

    /// <summary>
    /// Applying a project that declares nothing, which drops what was built.
    /// </summary>
    public required CliResult Teardown { get; init; }

    /// <summary>
    /// A plan against the emptied database, which should want nothing.
    /// </summary>
    public required CliResult TeardownVerification { get; init; }

    /// <summary>
    /// The writer emits what the formatter would have written.
    /// </summary>
    public bool Canonical => Format.ExitCode == 0;

    /// <summary>
    /// Nothing at all is left to do: NSchema agrees with its own description of the database.
    /// </summary>
    public bool RoundTrips => Verification.ExitCode == 0;

    /// <summary>
    /// The SQL NSchema renders builds the schema it described.
    /// </summary>
    public bool Rebuilds => RebuildVerification.ExitCode == 0;

    /// <summary>
    /// The engine agrees the rebuild holds the source's schema.
    /// Null when the rebuild did not complete, so there was nothing to compare.
    /// </summary>
    public bool? Faithful => EngineTestimony is null ? null : EngineTestimony.Count == 0;

    /// <summary>
    /// It comes apart again, in an order the engine accepts.
    /// </summary>
    public bool TearsDown => TeardownVerification.ExitCode == 0;

    /// <summary>
    /// Where the round trip first failed, in protocol order — or <see cref="CorpusOutcome.Succeeded"/>.
    /// </summary>
    public CorpusOutcome Outcome =>
        !Canonical ? CorpusOutcome.CanonicalFailed
        : !RoundTrips ? CorpusOutcome.RoundTripFailed
        : !Rebuilds ? CorpusOutcome.RebuildFailed
        : Faithful == false ? CorpusOutcome.FidelityFailed
        : !TearsDown ? CorpusOutcome.TeardownFailed
        : CorpusOutcome.Succeeded;

    /// <summary>
    /// Renders the artifacts for the snapshot. The verdicts and the expectation are asserted by the
    /// test, not pinned here — a snapshot diff should always mean a plan or a diagnostic changed.
    /// </summary>
    /// <remarks>
    /// The adoption plan is the one always worth recording: both its sides come from the same
    /// introspection, one of them by way of the writer and the parser, so it is the round trip made
    /// visible. Every later plan is empty by construction, so it appears only when it isn't — and then
    /// what it still wants to do is the finding.
    /// </remarks>
    public string Render()
    {
        var report = new StringBuilder();

        report.AppendLine("=== first plan against the imported project ===");
        report.AppendLine(Adoption.StandardOutput.Trim());

        if (Adoption.StandardError.Trim() is { Length: > 0 } diagnostics)
        {
            report.AppendLine(diagnostics);
        }

        Unfinished(report, "the project is not canonical", Canonical, Format);
        Unfinished(report, "left over after adopting", RoundTrips, Verification);
        Unfinished(report, "left over after rebuilding", Rebuilds, RebuildVerification);
        Unfinished(report, "left over after tearing down", TearsDown, TeardownVerification);

        if (EngineTestimony is { Count: > 0 })
        {
            report.AppendLine();
            report.AppendLine("=== the engine disagrees about the rebuild ===");
            foreach (var line in EngineTestimony)
            {
                report.AppendLine(line);
            }
        }

        return report.ToString();
    }

    // A leg that settled has nothing to say; one that did not is a finding, and what it still wants to do is it.
    private static void Unfinished(StringBuilder report, string stage, bool settled, CliResult result)
    {
        if (settled)
        {
            return;
        }

        report.AppendLine();
        report.AppendLine($"=== {stage} ===");
        report.AppendLine(result.StandardOutput.Trim());
        report.AppendLine(result.StandardError.Trim());
    }
}
