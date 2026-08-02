using NSchema.Gauntlet.Services;

namespace NSchema.Gauntlet.Tests;

/// <summary>
/// The cells a run consists of.
/// </summary>
public static class GauntletMatrix
{
    /// <summary>
    /// Every scenario against every engine.
    /// </summary>
    public static TheoryData<string, string> ScenariosAndEngines
    {
        get
        {
            var run = new GauntletRun();
            return CrossProduct(run.Scenarios, run.Engines);
        }
    }

    private static TheoryData<string, string> CrossProduct(ScenarioCatalog scenarios, EngineFleet engines)
    {
        var matrix = new TheoryData<string, string>();
        foreach (var scenario in scenarios.Names)
        {
            foreach (var engine in engines.Names)
            {
                matrix.Add(scenario, engine);
            }
        }
        return matrix;
    }
}
