namespace NSchema.Gauntlet.Services.Engines.SqlServer;

/// <summary>
/// One catalog relation the oracle reads.
/// </summary>
/// <param name="Name">The relation, which is what a finding is grouped under.</param>
/// <param name="Sql">
/// Returns the identity, the whole row as JSON, and optionally a second JSON object whose attributes
/// overlay the first.
/// </param>
/// <param name="Exclude">Attributes that legitimately differ between two databases holding the same schema.</param>
public sealed record SqlServerCatalog(string Name, string Sql, params string[] Exclude);