namespace NaturalQuery.RateLimiting;

/// <summary>
/// Rate limiter interface for controlling query throughput per tenant.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Checks if the request is allowed under the current rate limit.
    /// </summary>
    /// <param name="tenantId">Tenant identifier (or "global" for non-tenant requests).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if allowed, false if rate limit exceeded.</returns>
    Task<bool> IsAllowedAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Gets the number of remaining requests for the tenant in the current window.
    /// </summary>
    /// <param name="tenantId">Tenant identifier (or "global" for non-tenant requests).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of remaining allowed requests.</returns>
    Task<int> GetRemainingAsync(string tenantId, CancellationToken ct = default);
}
