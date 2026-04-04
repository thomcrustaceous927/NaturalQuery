using NaturalQuery.Models;

namespace NaturalQuery.Caching;

/// <summary>
/// Cache interface for storing NL2SQL query results.
/// Avoids redundant LLM calls for repeated questions.
/// </summary>
public interface IQueryCache
{
    /// <summary>Tries to get a cached result for the given question and tenant.</summary>
    /// <returns>The cached result, or null if not found.</returns>
    Task<QueryResult?> GetAsync(string question, string? tenantId, CancellationToken ct = default);

    /// <summary>Stores a result in the cache.</summary>
    Task SetAsync(string question, string? tenantId, QueryResult result, CancellationToken ct = default);

    /// <summary>Invalidates all cached entries for a tenant (or all if tenantId is null).</summary>
    Task InvalidateAsync(string? tenantId = null, CancellationToken ct = default);
}
