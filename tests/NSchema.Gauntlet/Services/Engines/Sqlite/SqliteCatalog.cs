namespace NSchema.Gauntlet.Services.Engines.Sqlite;

/// <summary>
/// One relation the Sqlite oracle reads, and how its rows are made comparable between two databases.
/// </summary>
/// <param name="Name">What the relation is called in a finding.</param>
/// <param name="Sql">A query whose every column is compared, except those named in <paramref name="Exclude"/>.</param>
/// <param name="Identity">The columns naming the object a row is about, joined with a dot.</param>
/// <param name="Exclude">Columns to leave out: physical facts, and ids that mean nothing across two databases.</param>
/// <remarks>
/// A query may also select <c>&lt;column&gt;__as</c> alongside <c>*</c> to compare that column as the expression
/// says rather than as it is stored — the one way to normalise a value without listing every other column and
/// losing the property that an unnamed column is still compared.
/// </remarks>
public sealed record SqliteCatalog(string Name, string Sql, string[] Identity, params string[] Exclude);
