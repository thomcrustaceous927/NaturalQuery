using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace NaturalQuery.RateLimiting;

/// <summary>
/// In-memory sliding window rate limiter. Tracks requests per tenant
/// in a 1-minute window. Thread-safe via ConcurrentDictionary.
/// </summary>
public class InMemoryRateLimiter : IRateLimiter
{
    private readonly int _maxPerMinute;
    private readonly ConcurrentDictionary<string, TenantWindow> _windows = new();

    /// <summary>Initializes the rate limiter with the configured limit from NaturalQueryOptions.</summary>
    public InMemoryRateLimiter(IOptions<NaturalQueryOptions> options)
    {
        _maxPerMinute = options.Value.RateLimitPerMinute > 0 ? options.Value.RateLimitPerMinute : 60;
    }

    /// <inheritdoc />
    public Task<bool> IsAllowedAsync(string tenantId, CancellationToken ct = default)
    {
        var key = tenantId ?? "global";
        var window = _windows.GetOrAdd(key, _ => new TenantWindow());

        lock (window)
        {
            window.CleanExpired();

            if (window.Timestamps.Count >= _maxPerMinute)
                return Task.FromResult(false);

            window.Timestamps.Add(DateTime.UtcNow);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<int> GetRemainingAsync(string tenantId, CancellationToken ct = default)
    {
        var key = tenantId ?? "global";
        if (!_windows.TryGetValue(key, out var window))
            return Task.FromResult(_maxPerMinute);

        lock (window)
        {
            window.CleanExpired();
            return Task.FromResult(Math.Max(0, _maxPerMinute - window.Timestamps.Count));
        }
    }

    private class TenantWindow
    {
        public List<DateTime> Timestamps { get; } = new();

        public void CleanExpired()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-1);
            Timestamps.RemoveAll(t => t < cutoff);
        }
    }
}
