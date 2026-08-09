using System.Text.Json;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Engines;

/// <summary>
/// One fact from an engine's catalog: a relation, the object it is about, an attribute, and its value.
/// </summary>
/// <remarks>
/// One row per attribute rather than per object, because it gives better information to diagnose issues.
/// </remarks>
public static class CatalogRow
{
    /// <summary>
    /// Flattens one catalog row's JSON into a line per attribute.
    /// </summary>
    /// <param name="catalog">The relation the row came from.</param>
    /// <param name="identity">The object the row is about.</param>
    /// <param name="json">The whole row, as the engine serialized it.</param>
    /// <param name="exclude">Attributes to leave out.</param>
    /// <param name="resolved">A JSON object to merge with the result. Used to convert arbitrary OIDs to names.</param>
    public static IEnumerable<CatalogFact> Flatten(string catalog, string identity, string json, IReadOnlySet<string>? exclude = null, string? resolved = null)
    {
        var overlay = Overlay(resolved);

        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (exclude?.Contains(property.Name) == true)
            {
                continue;
            }

            var value = overlay.Remove(property.Name, out var replacement) ? replacement : Value(property.Value);
            yield return new CatalogFact(catalog, identity, property.Name, value);
        }

        // Anything the overlay named that the row did not hold is still a fact worth reporting.
        foreach (var (name, value) in overlay)
        {
            yield return new CatalogFact(catalog, identity, name, value);
        }
    }

    private static Dictionary<string, string> Overlay(string? resolved)
    {
        var overlay = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(resolved))
        {
            return overlay;
        }

        using var document = JsonDocument.Parse(resolved);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return overlay;
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            overlay[property.Name] = Value(property.Value);
        }

        return overlay;
    }

    // Scalars read better bare; anything structured keeps its JSON, which compares exactly and is still
    // legible when it turns up in a diff.
    private static string Value(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => "NULL",
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => element.GetRawText(),
        _ => element.GetRawText(),
    };
}
