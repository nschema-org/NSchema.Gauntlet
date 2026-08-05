using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Scenarios;

/// <summary>
/// Every scenario the gauntlet can run.
/// </summary>
public sealed class ScenarioCatalog(string root, ScenarioCatalogSettings settings)
{
    // Web defaults give camelCase and case-insensitive properties, but not string enums — a manifest
    // naming its policy has to be told how to read one.
    private static readonly JsonSerializerOptions _manifest = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// The registered scenario names, in matrix order.
    /// </summary>
    public IEnumerable<ScenarioName> Names => Directory
        .EnumerateDirectories(CatalogDirectory())
        .Select(Path.GetFileName)
        .OfType<string>()
        .OrderBy(name => name, StringComparer.Ordinal)
        .Select(ScenarioName.From);

    /// <summary>
    /// Reads a scenario off disk.
    /// </summary>
    public Scenario Get(ScenarioName name)
    {
        var directory = Path.Combine(CatalogDirectory(), name.Value);
        var manifest = JsonSerializer.Deserialize<ScenarioManifest>(
            File.ReadAllText(Path.Combine(directory, settings.Manifest)),
            _manifest) ?? throw new InvalidOperationException($"Scenario '{name}' has an empty manifest.");

        return new Scenario
        {
            Name = name,
            Description = manifest.Description,
            BootstrapNsql = Nsql.From(File.ReadAllText(Path.Combine(directory, manifest.BeforeFile))),
            ScenarioNsql = Nsql.From(File.ReadAllText(Path.Combine(directory, manifest.AfterFile))),
            SeedSql = string.IsNullOrEmpty(manifest.DataFile) ? null : Sql.From(File.ReadAllText(Path.Combine(directory, manifest.DataFile))),
            DestructiveActions = manifest.DestructiveActions,
            Expectations = manifest.Expectations,
        };
    }

    private string CatalogDirectory() => Path.Combine(root, settings.Directory);

    private sealed record ScenarioManifest(
        string Description,
        DestructiveActionPolicy DestructiveActions,
        Dictionary<EngineName, ScenarioExpectation> Expectations,
        string BeforeFile = "before.nsql",
        string AfterFile = "after.nsql",
        string? DataFile = null
    );
}
