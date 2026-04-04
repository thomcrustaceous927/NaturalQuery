using FluentAssertions;
using Microsoft.Extensions.Options;
using NaturalQuery.RateLimiting;

namespace NaturalQuery.Tests;

public class InMemoryRateLimiterTests
{
    private InMemoryRateLimiter CreateLimiter(int maxPerMinute = 5)
    {
        var options = Options.Create(new NaturalQueryOptions { RateLimitPerMinute = maxPerMinute });
        return new InMemoryRateLimiter(options);
    }

    [Fact]
    public async Task First_Request_Should_Be_Allowed()
    {
        var limiter = CreateLimiter();

        var allowed = await limiter.IsAllowedAsync("tenant1");

        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Requests_Under_Limit_Should_Be_Allowed()
    {
        var limiter = CreateLimiter(maxPerMinute: 3);

        var r1 = await limiter.IsAllowedAsync("tenant1");
        var r2 = await limiter.IsAllowedAsync("tenant1");
        var r3 = await limiter.IsAllowedAsync("tenant1");

        r1.Should().BeTrue();
        r2.Should().BeTrue();
        r3.Should().BeTrue();
    }

    [Fact]
    public async Task Requests_Over_Limit_Should_Be_Rejected()
    {
        var limiter = CreateLimiter(maxPerMinute: 2);

        await limiter.IsAllowedAsync("tenant1");
        await limiter.IsAllowedAsync("tenant1");
        var third = await limiter.IsAllowedAsync("tenant1");

        third.Should().BeFalse();
    }

    [Fact]
    public async Task Different_Tenants_Should_Have_Independent_Limits()
    {
        var limiter = CreateLimiter(maxPerMinute: 1);

        var t1 = await limiter.IsAllowedAsync("tenant1");
        var t2 = await limiter.IsAllowedAsync("tenant2");
        var t1Again = await limiter.IsAllowedAsync("tenant1");

        t1.Should().BeTrue();
        t2.Should().BeTrue();
        t1Again.Should().BeFalse();
    }

    [Fact]
    public async Task GetRemaining_Should_Return_Full_Limit_For_New_Tenant()
    {
        var limiter = CreateLimiter(maxPerMinute: 10);

        var remaining = await limiter.GetRemainingAsync("new-tenant");

        remaining.Should().Be(10);
    }

    [Fact]
    public async Task GetRemaining_Should_Decrease_After_Requests()
    {
        var limiter = CreateLimiter(maxPerMinute: 5);

        await limiter.IsAllowedAsync("tenant1");
        await limiter.IsAllowedAsync("tenant1");
        var remaining = await limiter.GetRemainingAsync("tenant1");

        remaining.Should().Be(3);
    }

    [Fact]
    public async Task GetRemaining_Should_Never_Go_Below_Zero()
    {
        var limiter = CreateLimiter(maxPerMinute: 1);

        await limiter.IsAllowedAsync("tenant1");
        await limiter.IsAllowedAsync("tenant1"); // rejected but still

        var remaining = await limiter.GetRemainingAsync("tenant1");

        remaining.Should().Be(0);
    }

    [Fact]
    public async Task Default_RateLimitPerMinute_Should_Be_60_When_Zero()
    {
        var options = Options.Create(new NaturalQueryOptions { RateLimitPerMinute = 0 });
        var limiter = new InMemoryRateLimiter(options);

        // Should allow 60 requests (default)
        var remaining = await limiter.GetRemainingAsync("t1");
        remaining.Should().Be(60);
    }
}
