namespace NSchema.Gauntlet.Model;

/// <summary>
/// Declares the expected outcome for a given scenario running against a specific engine
/// </summary>
public sealed class ScenarioExpectation
{
    /// <summary>
    /// The outcome the engine is expected to produce.
    /// </summary>
    public required ScenarioOutcome Outcome { get; init; }

    /// <summary>
    /// The reason that <see cref="Outcome"/> is anything other than <see cref="ScenarioOutcome.Succeeded"/>.
    /// </summary>
    public string? Because { get; init; }
}
