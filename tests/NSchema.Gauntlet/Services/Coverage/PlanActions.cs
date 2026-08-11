using System.Text.Json;

namespace NSchema.Gauntlet.Services.Coverage;

/// <summary>
/// The migration actions a saved plan file performs.
/// </summary>
public static class PlanActions
{
    /// <summary>
    /// The distinct actions named by the plan at <paramref name="planFile"/>, or nothing if it holds none.
    /// </summary>
    public static IReadOnlyCollection<string> Read(string planFile)
    {
        if (!File.Exists(planFile))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(planFile));

            if (!document.RootElement.TryGetProperty("plan", out var plan)
                || !plan.TryGetProperty("statements", out var statements)
                || statements.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return
            [
                .. statements
                    .EnumerateArray()
                    .Select(statement => statement.TryGetProperty("action", out var action) ? action.GetString() : null)
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal),
            ];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
