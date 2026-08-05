namespace NSchema.Gauntlet.Extensions;

internal static class ErrorOrExtensions
{
    extension<T>(ErrorOr<T> errorOr)
    {
        /// <summary>
        /// Describes a result.
        /// </summary>
        /// <returns>One of: the error description, the value description, or a fallback message.</returns>
        public string Describe() => errorOr.IsError
            ? errorOr.FirstError.ToString()
            : errorOr.Value?.ToString() ?? "Result is not an error, and contains no Value.";
    }
}
