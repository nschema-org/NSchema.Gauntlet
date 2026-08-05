namespace NSchema.Gauntlet.Model;

/// <summary>
/// Declares the expected outcome for a given corpus or scenario running against a specific engine
/// </summary>
/// <remarks>This may or may not need refactoring into ScenarioExpectation and CorpusExpectation.</remarks>
public sealed class Expectation
{
    public required bool Blocks { get; set; }
}
