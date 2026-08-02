namespace NSchema.Gauntlet.Services.Engines;

/// <summary>
/// A provider package, as a project declares it.
/// </summary>
public sealed class PluginSettings
{
    public string Package { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// The PLUGIN statement declaring this provider under the given label.
    /// </summary>
    public string Declaration(string label) =>
        $"""
         PLUGIN {label} (
           source = '{Package}',
           version = '{Version}'
         );
         """;
}
