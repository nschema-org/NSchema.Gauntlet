namespace NSchema.Gauntlet.Model;

/// <summary>
/// An authored before/after pair, testing one capability across every engine.
/// </summary>
public sealed class Scenario
{
    /// <summary>
    /// The scenario's name.
    /// </summary>
    public required ScenarioName Name { get; init; }

    /// <summary>
    /// What the scenario is for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The declared schema the database starts at.
    /// </summary>
    public required Nsql BootstrapNsql { get; init; }

    /// <summary>
    /// Engine-native rows loaded once the before state is established, for hazards that only data triggers.
    /// </summary>
    public Sql? SeedSql { get; init; }

    /// <summary>
    /// The declared schema the database is moved to.
    /// </summary>
    public required Nsql ScenarioNsql { get; init; }

    /// <summary>
    /// How the destructive-action policy is enforced for the change under test.
    /// </summary>
    public required DestructiveActionPolicy DestructiveActions { get; init; }

    /// <summary>
    /// How every engine is expected to perform against this scenario.
    /// </summary>
    public IReadOnlyDictionary<EngineName, ScenarioExpectation> Expectations { get; init; } = new Dictionary<EngineName, ScenarioExpectation>();
}
