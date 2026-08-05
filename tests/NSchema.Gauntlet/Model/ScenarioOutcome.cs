namespace NSchema.Gauntlet.Model;

/// <summary>
/// The outcome of a scenario run against an engine.
/// </summary>
public enum ScenarioOutcome
{
    /// <summary>
    /// The change applies and the database moves to the scenario schema.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The bootstrap state is refused: the scenario's starting point is itself beyond the engine.
    /// </summary>
    BootstrapFailed,

    /// <summary>
    /// The bootstrap succeeds, but the change under test is refused.
    /// </summary>
    ChangeFailed,
}
