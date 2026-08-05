namespace NSchema.Gauntlet.Model;

/// <summary>
/// A real schema, acquired from outside, in each engine's own DDL.
/// </summary>
public sealed class Corpus
{
    /// <summary>
    /// The name of the corpus.
    /// </summary>
    public required CorpusName Name { get; init; }

    /// <summary>
    /// What the schema is, and why it is worth round-tripping.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The DDL that establishes the schema.
    /// </summary>
    /// <remarks>A corpus only supplies DDL for the engine(s) it was built for.</remarks>
    public required IReadOnlyDictionary<EngineName, Sql> Ddl { get; init; }
}
