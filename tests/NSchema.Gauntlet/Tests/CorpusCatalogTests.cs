using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Services.Corpus;

namespace NSchema.Gauntlet.Tests;

public sealed class CorpusCatalogTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nschema-gauntlet-catalog-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Get_ExpectationForAnEngineWithoutDdl_Throws()
    {
        // Arrange — the manifest expects an engine the corpus supplies no DDL for: a dead declaration.
        var corpus = Directory.CreateDirectory(Path.Combine(_root, "corpus", "phantom"));
        File.WriteAllText(Path.Combine(corpus.FullName, "postgres.sql"), "CREATE TABLE t (id int);");
        File.WriteAllText(Path.Combine(corpus.FullName, "manifest.json"),
            """
            {
              "description": "A corpus expecting an engine it cannot run on.",
              "expectations": {
                "postgres": { "outcome": "succeeded" },
                "sqlite": { "outcome": "succeeded" }
              }
            }
            """);
        var catalog = new CorpusCatalog(_root, new CorpusCatalogSettings());

        // Act
        var reading = () => catalog.Get(CorpusName.From("phantom"));

        // Assert
        reading.ShouldThrow<InvalidOperationException>().Message.ShouldContain("'sqlite'");
    }
}
