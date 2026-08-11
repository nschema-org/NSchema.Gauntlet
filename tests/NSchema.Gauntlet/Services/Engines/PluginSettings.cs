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
    /// The version the project declares. Ignored when <see cref="Project"/> is set.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// A provider project to use instead of restoring the package.
    /// </summary>
    public string? Project { get; init; }

    /// <summary>
    /// The assembly the run built <see cref="Project"/> into, or <see langword="null"/> until it has.
    /// </summary>
    public string? Assembly { get; private set; }

    /// <summary>
    /// Records where the run built <see cref="Project"/> to.
    /// Called once, during initialization, before anything reads a declaration.
    /// </summary>
    public void BuiltAt(string assembly) => Assembly = assembly;

    /// <summary>
    /// The <c>PLUGIN</c> declaration this settles on, ready to write into a project.
    /// </summary>
    public string Declaration(string label) => Assembly is { Length: > 0 } assembly
        ? $"""
           PLUGIN {label} (
             path = '{assembly.Replace("'", "''")}'
           );
           """
        : $"""
           PLUGIN {label} (
             source = '{Package}',
             version = '{Version}'
           );
           """;

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
