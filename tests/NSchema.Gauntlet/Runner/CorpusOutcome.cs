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
    /// The writer emits what the formatter would have written.
    /// </summary>
    public bool Canonical => Format?.ExitCode == 0;

    /// <summary>
    /// Nothing at all is left to do: NSchema agrees with its own description of the database.
    /// </summary>
    public bool RoundTrips => Verification?.ExitCode == 0;

    public static CorpusOutcome Failed(CliResult setup) => new() { SetupFailure = setup };

    /// <summary>
    /// The snapshot surface. The imported project itself is not included.
    /// </summary>
    public string Report()
    {
        var report = new StringBuilder();

        report.AppendLine($"canonical: {Canonical}");
        report.AppendLine($"round-trips: {RoundTrips}");
        report.AppendLine();
        report.AppendLine("=== first plan against the imported project ===");
        report.AppendLine(Adoption?.StandardOutput.Trim());

        if (Adoption?.StandardError.Trim() is { Length: > 0 } diagnostics)
        {
            report.AppendLine(diagnostics);
        }

        return report.ToString();
    }
}
