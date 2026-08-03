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
            var matrix = new TheoryData<string, string>();
            var scenarios = run.Scenarios.Names.ToList();
            var engines = run.Engines.Names.ToList();
            foreach (var scenario in scenarios)
            {
                foreach (var engine in engines)
                {
                    matrix.Add(scenario, engine);
                }
            }
            return matrix;
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
            var matrix = new TheoryData<string, string>();

            foreach (var corpusName in run.Corpus.Names)
            {
                var corpus = run.Corpus.Get(corpusName);
                foreach (var engine in corpus.Ddl.Keys)
                {
                    matrix.Add(corpusName, engine);
                }
            }

            return matrix;
        }
    }
}
