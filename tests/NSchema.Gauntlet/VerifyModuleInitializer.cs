using System.Runtime.CompilerServices;

namespace NSchema.Gauntlet;

internal static class VerifyModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        DerivePathInfo((sourceFile, _, type, method) => new PathInfo(
            directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
            typeName: type.Name,
            methodName: method.Name
        ));

        VerifierSettings.ScrubLinesContaining("nschema-gauntlet");
        VerifierSettings.ScrubLinesContaining("Host=");
        VerifierSettings.ScrubInlineDateTimes("yyyy-MM-ddTHH:mm:ss");
    }
}
