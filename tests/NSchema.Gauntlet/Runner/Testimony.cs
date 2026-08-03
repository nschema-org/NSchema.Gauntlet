namespace NSchema.Gauntlet.Runner;

/// <summary>
/// The disagreement between two engines' accounts of their schemas — the oracle outside NSchema. Every
/// NSchema-vs-NSchema leg is blind to a consistent introspection error; the catalogs are not.
/// </summary>
public static class Testimony
{
    /// <summary>
    /// The rows the two accounts do not share, each labelled with the side that lacks it. Empty means the
    /// engine agrees the rebuild holds the source's schema.
    /// </summary>
    public static IReadOnlyList<string> Differences(IReadOnlyList<string> source, IReadOnlyList<string> rebuild)
    {
        // A multiset diff: two system-named constraints may testify identically, and losing one must count.
        var counts = new Dictionary<string, int>();
        foreach (var row in source)
        {
            counts[row] = counts.GetValueOrDefault(row) + 1;
        }
        foreach (var row in rebuild)
        {
            counts[row] = counts.GetValueOrDefault(row) - 1;
        }

        return [.. counts
            .Where(count => count.Value != 0)
            .Select(count => $"{(count.Value > 0 ? "missing from the rebuild" : "only in the rebuild")}: {count.Key}")
            .OrderBy(line => line, StringComparer.Ordinal)];
    }
}
