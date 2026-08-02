namespace NSchema.Gauntlet.Model;

/// <summary>
/// An authored before/after pair, testing one capability across every engine.
/// </summary>
public sealed record Scenario
{
    /// <summary>
    /// The scenario's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What the scenario is for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The declared schema the database starts at. A null state is an empty schema.
    /// </summary>
    public required string? BeforeNsql { get; init; }

    /// <summary>
    /// The declared schema the database is moved to. A null state is an empty schema.
    /// </summary>
    public required string? AfterNsql { get; init; }

    /// <summary>
    /// Engine-native rows loaded once the before state is established, for hazards that only data triggers.
    /// </summary>
    public string? DataSql { get; init; }

    /// <summary>
    /// How the destructive-action policy is enforced for the change under test.
    /// </summary>
    public string? DestructiveActions { get; init; }
}
