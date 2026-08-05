using NSchema.Gauntlet.Services.Cli;

namespace NSchema.Gauntlet.Runner;

/// <summary>
/// One CLI invocation of the run, under the name the scenario protocol gives it.
/// </summary>
public sealed record ScenarioStage(StageName Name, CliResult Result);
