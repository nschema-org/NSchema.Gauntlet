using NSchema.Gauntlet.Services;

namespace NSchema.Gauntlet.Tests;

/// <summary>
/// The cells a run consists of.
/// </summary>
/// <remarks>
/// One test per cell, so an engine that fails a case fails visibly rather than inside a case named after
/// something else, and so a run can be filtered to one engine. Enumerating it starts nothing.
/// </remarks>
public static class GauntletMatrix
{
    /// <summary>
    /// Every scenario against every engine.
    /// </summary>
    public static TheoryData<string, string> ScenariosAndEngines => Across();

    private static TheoryData<string, string> Across()
    {
        var matrix = new TheoryData<string, string>();
        foreach (var name in ScenarioCatalog.Names)
        {
            foreach (var engine in EngineCatalog.Names)
            {
                matrix.Add(name, engine);
            }
        }
        return matrix;
    }
}
