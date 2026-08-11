using System.Collections.Concurrent;
using System.Text;
using NSchema.Gauntlet.Model;

namespace NSchema.Gauntlet.Services.Coverage;

/// <summary>
/// Which migration actions a run exercised, by engine.
/// </summary>
public sealed class ActionLedger
{
    private readonly ConcurrentDictionary<EngineName, ConcurrentDictionary<string, byte>> _exercised = new();
    private int _plans;

    /// <summary>
    /// Records the actions one plan performed against one engine.
    /// </summary>
    public void Record(EngineName engine, IReadOnlyCollection<string> actions)
    {
        Interlocked.Increment(ref _plans);

        var seen = _exercised.GetOrAdd(engine, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));

        foreach (var action in actions)
        {
            seen[action] = 0;
        }
    }

    /// <summary>
    /// Whether anything was recorded, which a filtered run may well not have.
    /// </summary>
    public bool IsEmpty => _plans == 0;

    /// <summary>
    /// Renders the matrix against <paramref name="surface"/>, the actions the engine under test can produce.
    /// </summary>
    /// <param name="surface">Every concrete action, read from the engine's own assembly.</param>
    /// <param name="engines">The engines to report, in order, whether or not they were exercised.</param>
    public string Describe(IReadOnlyList<string> surface, IReadOnlyList<EngineName> engines)
    {
        var report = new StringBuilder();

        report.AppendLine("# Action coverage");
        report.AppendLine();
        report.AppendLine(
            $"{surface.Count} actions x {engines.Count} engines, from {_plans} plan(s). An action is marked where a plan " +
            "the run wrote named it.");
        report.AppendLine();

        // A blank cell is "this run never exercised it", which is not the same as "this engine cannot". Telling
        // those apart means asking the dialect what it refuses, and a plan that never reaches an action says
        // nothing either way — so the report claims only what it saw.
        report.AppendLine("A blank cell means this run did not exercise that action, which may be because the engine");
        report.AppendLine("does not support it or because nothing here asks for it. The report does not distinguish them.");
        report.AppendLine();

        report.AppendLine($"| Action | {string.Join(" | ", engines.Select(engine => engine.Value))} |");
        report.AppendLine($"|---|{string.Concat(engines.Select(_ => "---|"))}");

        foreach (var action in surface)
        {
            var cells = engines.Select(engine => Exercised(engine, action) ? "x" : " ");
            report.AppendLine($"| {action} | {string.Join(" | ", cells)} |");
        }

        report.AppendLine();

        foreach (var engine in engines)
        {
            var covered = surface.Count(action => Exercised(engine, action));
            report.AppendLine($"- {engine.Value}: {covered}/{surface.Count} exercised.");
        }

        // Anything recorded that the surface does not list would mean the two halves were read from different
        // builds of NSchema, which is worth saying out loud rather than quietly dropping from the table.
        var unknown = _exercised
            .SelectMany(engine => engine.Value.Keys)
            .Distinct(StringComparer.Ordinal)
            .Except(surface, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (unknown.Count > 0)
        {
            report.AppendLine();
            report.AppendLine(
                $"Exercised but absent from the action surface: {string.Join(", ", unknown)}. " +
                "The plans and the surface were read from different builds.");
        }

        return report.ToString();
    }

    private bool Exercised(EngineName engine, string action) =>
        _exercised.TryGetValue(engine, out var seen) && seen.ContainsKey(action);
}
