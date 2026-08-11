using NSchema.Gauntlet.Services;
using NSchema.Gauntlet.Services.Coverage;

namespace NSchema.Gauntlet.Tests;

/// <summary>
/// The denominator a coverage report divides by, read from the engine the run drives.
/// </summary>
public sealed class ActionSurfaceTests(GauntletRun run)
{
    [Fact]
    public async Task Read_ReturnsTheEnginesOwnMigrationActions()
    {
        // Arrange — a run pinned to a package has not installed the CLI until something asks for it, and the
        // engine assembly is what is being read here rather than the engine's behaviour.
        var cli = await run.Cli.ResolveDirectory(TestContext.Current.CancellationToken);

        // Act
        var actions = ActionSurface.Read(cli);

        // Assert — named rather than counted. A count would pin the number of actions NSchema happens to have
        // today, so adding one would fail this for no reason; these are load-bearing enough that their absence
        // means the surface was read from the wrong place, or not read at all.
        actions.ShouldContain("CreateTable");
        actions.ShouldContain("DropTable");
        actions.ShouldContain("AddForeignKey");
        actions.ShouldContain("ExecuteScript");

        // The base type is abstract and is not an action anyone can perform.
        actions.ShouldNotContain("MigrationAction");

        // Enough of them to be the whole surface rather than a stray corner of it.
        actions.Count.ShouldBeGreaterThan(50);
    }
}
