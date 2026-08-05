namespace NSchema.Gauntlet.Model;

/// <summary>
/// Declares the expected outcome for a given corpus running against a specific engine.
/// </summary>
public sealed class CorpusExpectation
{
    /// <summary>
    /// The outcome the engine is expected to produce.
    /// </summary>
    public required CorpusOutcome Outcome { get; init; }

    /// <summary>
    /// The reason that <see cref="Outcome"/> is anything other than <see cref="CorpusOutcome.Succeeded"/>.
    /// </summary>
    public string? Because { get; init; }
}
