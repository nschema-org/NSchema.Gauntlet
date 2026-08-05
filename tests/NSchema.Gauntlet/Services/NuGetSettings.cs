namespace NSchema.Gauntlet.Services;

/// <summary>
/// The settings to use when configuring NuGet.
/// </summary>
public sealed class NuGetSettings
{
    /// <summary>
    /// The additional sources from which packages and tools can be installed.
    /// </summary>
    public string[] Sources { get; init; } = [];


    /// <summary>
    /// Writes a NuGet.Config file to the given directory.
    /// </summary>
    public void WriteConfig(string directory)
    {
        var file = Path.Combine(directory, "NuGet.Config");
        var config = $"""
                      <configuration>
                        <packageSources>
                          {string.Join("\n", Sources.Select((source, i) => $"""    <add key="gauntlet-{i}" value="{source}" />"""))}
                        </packageSources>
                      </configuration>
                      """;
        File.WriteAllText(file, config);
    }
}
