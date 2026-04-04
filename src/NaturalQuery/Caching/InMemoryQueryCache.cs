using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NaturalQuery.Models;

namespace NaturalQuery.Caching;

/// <summary>
/// In-memory cache implementation using Microsoft.Extensions.Caching.Memory.
/// Cache key is a SHA256 hash of (question + tenantId) for privacy.
/// </summary>
public class InMemoryQueryCache : IQueryCache
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    /// <summary>Initializes the cache with the configured TTL from NaturalQueryOptions.</summary>
    public InMemoryQueryCache(IMemoryCache cache, IOptions<NaturalQueryOptions> options)
    {
        _cache = cache;
        _ttl = TimeSpan.FromMinutes(options.Value.CacheTtlMinutes > 0 ? options.Value.CacheTtlMinutes : 5);
    }

    /// <inheritdoc />
    public Task<QueryResult?> GetAsync(string question, string? tenantId, CancellationToken ct = default)
    {
        var key = BuildKey(question, tenantId);
        _cache.TryGetValue(key, out QueryResult? result);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task SetAsync(string question, string? tenantId, QueryResult result, CancellationToken ct = default)
    {
        var key = BuildKey(question, tenantId);
        _cache.Set(key, result, _ttl);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task InvalidateAsync(string? tenantId = null, CancellationToken ct = default)
    {
        // MemoryCache doesn't support key enumeration natively.
        // For full invalidation, consider using a distributed cache.
        // This is a best-effort implementation for in-memory scenarios.
        if (_cache is MemoryCache mc)
            mc.Compact(1.0);
        return Task.CompletedTask;
    }

    private static string BuildKey(string question, string? tenantId)
    {
        var raw = $"nq:{tenantId ?? "global"}:{question.Trim().ToLowerInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}
