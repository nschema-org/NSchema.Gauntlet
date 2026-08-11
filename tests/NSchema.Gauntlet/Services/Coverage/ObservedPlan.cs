using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Services.Coverage;

/// <summary>
/// A plan the run needed, and the actions it turned out to perform.
/// </summary>
/// <param name="Result">The plan as the CLI reported it.</param>
/// <param name="Actions">The actions the plan performs, read from the plan NSchema saved.</param>
public sealed record ObservedPlan(CliResult Result, IReadOnlyCollection<string> Actions);
