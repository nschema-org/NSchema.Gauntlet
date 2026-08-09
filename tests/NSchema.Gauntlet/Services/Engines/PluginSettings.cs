using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Engines;

/// <summary>
/// A provider plugin, as a project declares it.
/// </summary>
public sealed class PluginSettings
{
    /// <summary>
    /// The package the project declares.
    /// </summary>
    public required string Package { get; init; }

    /// <summary>
    /// The version the project declares. With <see cref="Project"/> set this is a label rather than a released
    /// version — it only has to name the cache entry the run is about to write.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// A provider project to build into the plugin cache before the run, instead of restoring the package.
    /// </summary>
    public string? Project { get; init; }

    /// <summary>
    /// The <c>PLUGIN</c> declaration this settles on, ready to write into a project.
    /// </summary>
    public string Declaration(string label) =>
        $"""
         PLUGIN {label} (
           source = '{Package}',
           version = '{Version}'
         );
         """;

    /// <summary>
    /// How this plugin should be described in a report.
    /// </summary>
    public string Describe() => Project is { Length: > 0 } project
        ? $"{Package} {Version} (built from {project})"
        : $"{Package} {Version}";

    /// <summary>
    /// Fails when a configured project does not exist.
    /// </summary>
    public void Validate(EngineName engine)
    {
        if (Project is { Length: > 0 } && !File.Exists(Project))
        {
            throw new InvalidOperationException(
                $"The '{engine}' plugin project '{Project}' does not exist. It must name a provider's .csproj.");
        }
    }
}
