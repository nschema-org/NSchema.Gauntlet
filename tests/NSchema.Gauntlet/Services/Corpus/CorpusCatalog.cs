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
    public IEnumerable<CorpusName> Names => Directory
        .EnumerateDirectories(CatalogDirectory())
        .Select(Path.GetFileName)
        .OfType<string>()
        .OrderBy(name => name)
        .Select(CorpusName.From);

    /// <summary>
    /// Reads a case off disk.
    /// </summary>
    public Model.Corpus Get(CorpusName name)
    {
        var directory = Path.Combine(CatalogDirectory(), name.Value);
        var manifest = JsonSerializer.Deserialize<CorpusManifest>(
            File.ReadAllText(Path.Combine(directory, settings.Manifest)),
            _manifest) ?? throw new InvalidOperationException($"Corpus case '{name}' has an empty manifest.");

        var ddl = Directory
            .EnumerateFiles(directory, "*.sql")
            .ToDictionary(
                file => EngineName.From(Path.GetFileNameWithoutExtension(file)),
                file => Sql.From(File.ReadAllText(file))
            );

        return new Model.Corpus
        {
            Name = name,
            Description = manifest.Description,
            Ddl = ddl,
        };
    }

    private string CatalogDirectory() => Path.Combine(root, settings.Directory);

    private sealed record CorpusManifest(string Description);
}
