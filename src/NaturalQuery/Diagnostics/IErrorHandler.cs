namespace NaturalQuery.Diagnostics;

/// <summary>
/// Interface for handling NL2SQL errors. Implement this to integrate
/// with your logging/monitoring system (e.g., persist to database, send to Sentry).
/// </summary>
public interface IErrorHandler
{
    /// <summary>
    /// Called when an error occurs during NL2SQL processing.
    /// This method should not throw exceptions.
    /// </summary>
    Task HandleAsync(NaturalQueryError error, CancellationToken ct = default);
}
