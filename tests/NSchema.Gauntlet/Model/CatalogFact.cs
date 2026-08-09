namespace NSchema.Gauntlet.Model;

/// <summary>
/// One thing an engine's catalog says about one object.
/// </summary>
/// <param name="Catalog">The relation it came from: <c>pg_class</c>, <c>sys.indexes</c>.</param>
/// <param name="Identity">The object it is about, named the way a huamn would name it.</param>
/// <param name="Attribute">The attribute it reports.</param>
/// <param name="Value">What that attribute holds, rendered as text so two engines compare the same way.</param>
public readonly record struct CatalogFact(string Catalog, string Identity, string Attribute, string Value)
{
    /// <summary>
    /// What makes this fact the same fact in another database: everything but the value.
    /// </summary>
    public (string Catalog, string Identity, string Attribute) Address => (Catalog, Identity, Attribute);
}
