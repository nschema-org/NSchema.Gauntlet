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
            return CrossProduct(run.Scenarios.Names, run.Engines.Names);
        }
    }

    /// <summary>
    /// Every corpus case against the engines it was acquired for.
    /// </summary>
    public static TheoryData<string, string> CorpusAndEngines
    {
        get
        {
            var run = new GauntletRun();
            return CrossProduct(run.Corpus.Names, run.Engines.Names);
        }
    }

    private static TheoryData<string, string> CrossProduct(IEnumerable<string> left, IEnumerable<string> right)
    {
        var matrix = new TheoryData<string, string>();
        var rightList = right.ToList();
        foreach (var l in left)
        {
            foreach (var r in rightList)
            {
                matrix.Add(l, r);
            }
        }
        return matrix;
    }
}
