using System.Text;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Runner;

/// <summary>
/// The disagreement between two engines' accounts of their schemas — the oracle outside NSchema. Every
/// NSchema-vs-NSchema leg is blind to a consistent introspection error; the catalogs are not.
/// </summary>
public static class Testimony
{
    private const int Representatives = 3;

    private const string Absent = "(absent)";

    /// <summary>
    /// The symptoms the two accounts disagree on. Empty means the engine agrees the rebuild holds the
    /// source's schema.
    /// </summary>
    public static IReadOnlyList<CatalogDifference> Differences(IReadOnlyList<CatalogFact> source, IReadOnlyList<CatalogFact> rebuild)
    {
        var left = Index(source);
        var right = Index(rebuild);

        var findings = new List<(string Catalog, string Attribute, CatalogChange Change, CatalogOccurrence Occurrence)>();

        foreach (var key in left.Keys.Union(right.Keys))
        {
            var inSource = Values(left, key);
            var inRebuild = Values(right, key);

            if (inSource.SequenceEqual(inRebuild, StringComparer.Ordinal))
            {
                continue;
            }

            var change =
                inSource.Count == 0 ? CatalogChange.OnlyInRebuild
                : inRebuild.Count == 0 ? CatalogChange.MissingFromRebuild
                : CatalogChange.Changed;

            findings.Add((key.Catalog, key.Attribute, change,
                new CatalogOccurrence(key.Identity, Join(inSource), Join(inRebuild))));
        }

        // Ordered by address rather than by size: a snapshot that reorders itself whenever an occurrence
        // count shifts turns a one-line change into an unreadable diff.
        return
        [
            .. findings
                .GroupBy(finding => (finding.Catalog, finding.Attribute, finding.Change))
                .Select(group => new CatalogDifference(
                    group.Key.Catalog,
                    group.Key.Attribute,
                    group.Key.Change,
                    [.. group.Select(finding => finding.Occurrence).OrderBy(o => o.Identity, StringComparer.Ordinal)]))
                .OrderBy(difference => difference.Catalog, StringComparer.Ordinal)
                .ThenBy(difference => difference.Attribute, StringComparer.Ordinal)
                .ThenBy(difference => difference.Change)
        ];
    }

    /// <summary>
    /// Renders the findings, each with its occurrence count and a few representatives.
    /// </summary>
    public static string Describe(IReadOnlyList<CatalogDifference> differences)
    {
        if (differences.Count == 0)
        {
            return string.Empty;
        }

        var report = new StringBuilder();
        var facts = differences.Sum(difference => difference.Occurrences.Count);

        report.AppendLine($"{differences.Count} finding(s) across {facts} disagreeing fact(s).");

        foreach (var difference in differences)
        {
            report.AppendLine();
            report.AppendLine($"{difference.Catalog}.{difference.Attribute} — {Verb(difference.Change)}, on {difference.Occurrences.Count} object(s)");

            foreach (var occurrence in difference.Occurrences.Take(Representatives))
            {
                report.AppendLine($"    {occurrence.Identity}: {Value(difference.Change, occurrence)}");
            }

            if (difference.Occurrences.Count > Representatives)
            {
                report.AppendLine($"    ... and {difference.Occurrences.Count - Representatives} more");
            }
        }

        return report.ToString().TrimEnd();
    }

    private static string Verb(CatalogChange change) => change switch
    {
        CatalogChange.Changed => "changed",
        CatalogChange.MissingFromRebuild => "missing from the rebuild",
        _ => "only in the rebuild",
    };

    private static string Value(CatalogChange change, CatalogOccurrence occurrence) => change switch
    {
        CatalogChange.Changed => $"{occurrence.Source} -> {occurrence.Rebuild}",
        CatalogChange.MissingFromRebuild => occurrence.Source,
        _ => occurrence.Rebuild,
    };

    private static string Join(List<string> values) => values switch
    {
        [] => Absent,
        [var only] => only,
        _ => $"[{string.Join(", ", values)}]",
    };

    private static List<string> Values(
        Dictionary<(string Catalog, string Identity, string Attribute), List<string>> facts,
        (string Catalog, string Identity, string Attribute) key) =>
        facts.TryGetValue(key, out var values) ? values : [];

    // Facts are gathered by address rather than keyed by it, because one address can hold several: a table
    // inheriting from two parents reports a parent twice. Losing one of them has to count, so the values are
    // compared as a set and sorted to make that comparison order-independent.
    private static Dictionary<(string Catalog, string Identity, string Attribute), List<string>> Index(IReadOnlyList<CatalogFact> facts)
    {
        var index = new Dictionary<(string Catalog, string Identity, string Attribute), List<string>>();

        foreach (var fact in facts)
        {
            if (!index.TryGetValue(fact.Address, out var values))
            {
                index[fact.Address] = values = [];
            }

            values.Add(fact.Value);
        }

        foreach (var values in index.Values)
        {
            values.Sort(StringComparer.Ordinal);
        }

        return index;
    }
}
