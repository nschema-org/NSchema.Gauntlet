namespace NSchema.Gauntlet.Model;

/// <summary>
/// The outcome of a corpus round trip against an engine.
/// </summary>
public enum CorpusOutcome
{
    /// <summary>
    /// Every leg held: described, rebuilt, testified, and torn down.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The imported project is not what the formatter would write.
    /// </summary>
    CanonicalFailed,

    /// <summary>
    /// NSchema disagrees with its own description of the source database.
    /// </summary>
    RoundTripFailed,

    /// <summary>
    /// The SQL NSchema rendered did not build the schema it described.
    /// </summary>
    RebuildFailed,

    /// <summary>
    /// The engine's own account of the rebuild differs from the source.
    /// </summary>
    FidelityFailed,

    /// <summary>
    /// The schema would not come apart again.
    /// </summary>
    TeardownFailed,
}
