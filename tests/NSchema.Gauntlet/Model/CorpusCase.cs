namespace NSchema.Gauntlet.Model;

/// <summary>
/// A real schema, acquired from outside, in each engine's own DDL.
/// </summary>
public sealed record CorpusCase
{
    /// <summary>
    /// The case's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What the schema is, and why it is worth round-tripping.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The DDL that establishes the schema, keyed by engine. A case supplies it for the engines it has;
    /// the rest are absent from the matrix rather than failing in it.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Ddl { get; init; }
}
