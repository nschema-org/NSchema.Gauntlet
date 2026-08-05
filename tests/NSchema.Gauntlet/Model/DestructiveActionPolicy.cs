namespace NSchema.Gauntlet.Model;

/// <summary>
/// The destructive action policy to use.
/// </summary>
public enum DestructiveActionPolicy
{
    /// <summary>
    /// Destructive actions will be reported as errors.
    /// </summary>
    Error,

    /// <summary>
    /// Destructive actions will be reported as warnings.
    /// </summary>
    Warn,

    /// <summary>
    /// Destructive actions will be reported as info.
    /// </summary>
    Allow,

    /// <summary>
    /// Destructive actions will not be reported.
    /// </summary>
    Ignore,
}
