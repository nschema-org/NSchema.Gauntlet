using System.Runtime.CompilerServices;

namespace NSchema.Gauntlet;

internal static class VerifyModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifierSettings.ScrubLinesContaining("nschema-gauntlet");
        VerifierSettings.ScrubLinesContaining("Host=");
        VerifierSettings.ScrubInlineDateTimes("yyyy-MM-ddTHH:mm:ss");
    }
}
