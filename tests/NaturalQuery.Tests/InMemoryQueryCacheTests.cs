using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NaturalQuery.Caching;
using NaturalQuery.Models;

namespace NaturalQuery.Tests;

public class InMemoryQueryCacheTests
{
    private InMemoryQueryCache CreateCache(int ttlMinutes = 5)
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new NaturalQueryOptions { CacheTtlMinutes = ttlMinutes });
        return new InMemoryQueryCache(memoryCache, options);
    }

    private static QueryResult CreateResult(string title = "Test") => new()
    {
        Sql = "SELECT * FROM users",
        ChartType = "table",
        Title = title,
        Description = "Test result"
    };

    [Fact]
    public async Task GetAsync_Should_Return_Null_For_Missing_Key()
    {
        var cache = CreateCache();

        var result = await cache.GetAsync("unknown question", "tenant1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_Then_GetAsync_Should_Return_Cached_Value()
    {
        var cache = CreateCache();
        var expected = CreateResult("Cached");

        await cache.SetAsync("list users", "tenant1", expected);
        var result = await cache.GetAsync("list users", "tenant1");

        result.Should().NotBeNull();
        result!.Title.Should().Be("Cached");
        result.Sql.Should().Be("SELECT * FROM users");
    }

    [Fact]
    public async Task Different_Tenants_Should_Have_Different_Cache_Entries()
    {
        var cache = CreateCache();

        await cache.SetAsync("list users", "tenant1", CreateResult("Tenant1"));
        await cache.SetAsync("list users", "tenant2", CreateResult("Tenant2"));

        var result1 = await cache.GetAsync("list users", "tenant1");
        var result2 = await cache.GetAsync("list users", "tenant2");

        result1!.Title.Should().Be("Tenant1");
        result2!.Title.Should().Be("Tenant2");
    }

    [Fact]
    public async Task Same_Question_Different_Tenant_Should_Not_Collide()
    {
        var cache = CreateCache();

        await cache.SetAsync("count users", "tenantA", CreateResult("A"));

        var result = await cache.GetAsync("count users", "tenantB");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Null_TenantId_Should_Work()
    {
        var cache = CreateCache();

        await cache.SetAsync("list users", null, CreateResult("Global"));
        var result = await cache.GetAsync("list users", null);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Global");
    }

    [Fact]
    public async Task InvalidateAsync_Should_Clear_Cache()
    {
        var cache = CreateCache();

        await cache.SetAsync("list users", "t1", CreateResult());
        await cache.InvalidateAsync("t1");

        var result = await cache.GetAsync("list users", "t1");

        // MemoryCache.Compact(1.0) removes all entries
        result.Should().BeNull();
    }

    [Fact]
    public async Task Cache_Key_Should_Be_Case_Insensitive_On_Question()
    {
        var cache = CreateCache();

        await cache.SetAsync("List Users", "t1", CreateResult("Original"));
        var result = await cache.GetAsync("list users", "t1");

        // InMemoryQueryCache normalizes to lowercase
        result.Should().NotBeNull();
        result!.Title.Should().Be("Original");
    }
}
