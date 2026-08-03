using System.Text.Json;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Corpus;

/// <summary>
/// Every corpus case the gauntlet can run.
/// </summary>
public sealed class CorpusCatalog(string root, CorpusCatalogSettings settings)
{
    private static readonly JsonSerializerOptions _manifest = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The registered case names, in matrix order.
    /// </summary>
    public IEnumerable<string> Names => Directory
        .EnumerateDirectories(CatalogDirectory())
        .Select(Path.GetFileName)
        .OfType<string>()
        .OrderBy(name => name, StringComparer.Ordinal);

    /// <summary>
    /// Reads a case off disk.
    /// </summary>
    public CorpusCase Get(string name)
    {
        var directory = Path.Combine(CatalogDirectory(), name);
        var manifest = JsonSerializer.Deserialize<CorpusManifest>(
            File.ReadAllText(Path.Combine(directory, settings.Manifest)),
            _manifest) ?? throw new InvalidOperationException($"Corpus case '{name}' has an empty manifest.");

        var ddl = Directory
            .EnumerateFiles(directory, "*.sql")
            .ToDictionary(file => Path.GetFileNameWithoutExtension(file), File.ReadAllText, StringComparer.OrdinalIgnoreCase);

        return new CorpusCase
        {
            Name = name,
            Description = manifest.Description,
            Ddl = ddl,
        };
    }

    private string CatalogDirectory() => Path.Combine(root, settings.Directory);

    private sealed record CorpusManifest(string Description);
}
