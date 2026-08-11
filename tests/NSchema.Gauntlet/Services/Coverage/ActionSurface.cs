using System.Reflection;
using System.Runtime.InteropServices;

namespace NSchema.Gauntlet.Services.Coverage;

/// <summary>
/// Every migration action the engine under test can produce, read from that engine's own assembly.
/// </summary>
/// <remarks>
/// <para>
/// This is the denominator a coverage report divides by, and where it comes from is the whole point. It cannot be
/// derived from a plan, which only ever contains what happened; nor from the shape of the plan's JSON, which was
/// the first attempt and was wrong — actions that behave polymorphically never name themselves there. Nor is it a
/// list kept here, which would be a second copy of the truth, silently correct until someone adds an action.
/// </para>
/// <para>
/// So it is read from the <c>NSchema.Core.dll</c> the run is actually driving. A denominator taken from the same
/// binary as the numerator cannot describe a different version of NSchema than the one under test, which a list
/// maintained anywhere else eventually would.
/// </para>
/// <para>
/// Read through a <see cref="MetadataLoadContext"/> rather than by loading the assembly: the Gauntlet drives the
/// CLI as a process and deliberately does not reference Core, so there is no version of these types already in
/// this process to unify with, and nothing here should run engine code to ask it a question about its shape.
/// </para>
/// </remarks>
public static class ActionSurface
{
    private const string CoreAssembly = "NSchema.Core.dll";
    private const string ActionBaseType = "NSchema.Plan.Domain.MigrationAction";

    /// <summary>
    /// The name of every concrete migration action, ordered, as <see cref="Model.CatalogFact"/>-style stable text.
    /// </summary>
    /// <param name="cliDirectory">The directory the run's CLI was installed or built into.</param>
    public static IReadOnlyList<string> Read(string cliDirectory)
    {
        var core = Locate(cliDirectory);

        // The resolver needs the framework's own assemblies as well as the one being read: Core's types derive
        // from and reference types in System.Runtime, and a metadata context resolves nothing implicitly.
        var runtime = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        using var context = new MetadataLoadContext(new PathAssemblyResolver([.. runtime, core]));

        return
        [
            .. context.LoadFromAssemblyPath(core)
                .GetTypes()
                .Where(type => !type.IsAbstract && DerivesFromAction(type))
                .Select(type => type.Name)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Finds the engine assembly under <paramref name="cliDirectory"/>.
    /// </summary>
    /// <remarks>
    /// Searched for rather than composed, because the two ways a run acquires a CLI put it in different places: a
    /// build has it beside the executable, while <c>dotnet tool install</c> buries it under <c>.store</c>.
    /// </remarks>
    private static string Locate(string cliDirectory) =>
        Directory.EnumerateFiles(cliDirectory, CoreAssembly, SearchOption.AllDirectories).FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"Could not find '{CoreAssembly}' under '{cliDirectory}'. The action surface is read from the engine the run drives.");

    // Compared by name, not by type identity: the types in a metadata context are not the types in this one, so
    // there is nothing here to compare them against.
    private static bool DerivesFromAction(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.FullName == ActionBaseType)
            {
                return true;
            }
        }

        return false;
    }
}
