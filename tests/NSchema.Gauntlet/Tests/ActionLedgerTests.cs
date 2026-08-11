using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Coverage;

namespace NSchema.Gauntlet.Tests;

/// <summary>
/// The coverage matrix: what a run exercised, against what the engine could be asked to do.
/// </summary>
public sealed class ActionLedgerTests
{
    private static readonly EngineName _postgres = EngineName.From("postgres");
    private static readonly EngineName _sqlite = EngineName.From("sqlite");

    private static readonly string[] _surface = ["AddColumn", "CreateTable", "DropTable"];

    [Fact]
    public void Describe_MarksOnlyWhatEachEngineExercised()
    {
        // Arrange
        var ledger = new ActionLedger();
        ledger.Record(_postgres, ["CreateTable", "AddColumn"]);
        ledger.Record(_sqlite, ["CreateTable"]);

        // Act
        var report = ledger.Describe(_surface, [_postgres, _sqlite]);

        // Assert — one engine's coverage is not the other's, which is the whole reason the axis exists.
        report.ShouldContain("| CreateTable | x | x |");
        report.ShouldContain("| AddColumn | x |   |");
        report.ShouldContain("| DropTable |   |   |");
        report.ShouldContain("- postgres: 2/3 exercised.");
        report.ShouldContain("- sqlite: 1/3 exercised.");
    }

    [Fact]
    public void Describe_AnEngineThatRanNothing_IsReportedRatherThanOmitted()
    {
        // Arrange — an engine whose cases were filtered out still belongs in the table, or its absence reads as
        // full coverage to anyone scanning the rows.
        var ledger = new ActionLedger();
        ledger.Record(_postgres, ["CreateTable"]);

        // Act
        var report = ledger.Describe(_surface, [_postgres, _sqlite]);

        // Assert
        report.ShouldContain("- sqlite: 0/3 exercised.");
    }

    [Fact]
    public void Describe_AnActionOutsideTheSurface_SaysTheHalvesDisagree()
    {
        // Arrange — the numerator comes from plans the CLI wrote and the denominator from that CLI's own assembly,
        // so an action in one and not the other means they were read from different builds. Silently dropping it
        // would hide exactly the drift this harness keeps getting caught by.
        var ledger = new ActionLedger();
        ledger.Record(_postgres, ["CreateTable", "CreateSomethingNewer"]);

        // Act
        var report = ledger.Describe(_surface, [_postgres]);

        // Assert
        report.ShouldContain("CreateSomethingNewer");
        report.ShouldContain("different builds");
    }

    [Fact]
    public void IsEmpty_UntilSomethingIsRecorded()
    {
        // Arrange
        var ledger = new ActionLedger();

        // Assert — a run that recorded nothing has no report to write, rather than one claiming zero coverage.
        ledger.IsEmpty.ShouldBeTrue();

        ledger.Record(_postgres, []);
        ledger.IsEmpty.ShouldBeFalse();
    }
}
