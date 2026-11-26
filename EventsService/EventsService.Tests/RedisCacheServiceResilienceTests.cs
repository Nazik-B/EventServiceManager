using EventsService.Application.Dto;
using EventsService.Infrastructure.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace EventsService.Tests;

public sealed class RedisCacheServiceResilienceTests : IDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly RedisCacheService _cache;

    public RedisCacheServiceResilienceTests()
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = 500,
            SyncTimeout = 500,
            AsyncTimeout = 500
        };
        options.EndPoints.Add("127.0.0.1", 1);

        _redis = ConnectionMultiplexer.Connect(options);
        _cache = new RedisCacheService(_redis, NullLogger<RedisCacheService>.Instance);
    }

    [Fact]
    public async Task GetAsync_WhenRedisUnavailable_ReturnsNullWithoutThrowing()
    {
        Assert.False(_redis.IsConnected);

        var result = await _cache.GetAsync<EventResponse>("event:any");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_WhenRedisUnavailable_DoesNotThrow()
    {
        var error = await Record.ExceptionAsync(() =>
            _cache.SetAsync("event:any", new EventResponse { Title = "x" }, TimeSpan.FromSeconds(30)));

        Assert.Null(error);
    }

    [Fact]
    public async Task RemoveAsync_WhenRedisUnavailable_DoesNotThrow()
    {
        var error = await Record.ExceptionAsync(() => _cache.RemoveAsync("event:any"));

        Assert.Null(error);
    }

    [Fact]
    public async Task SetAsync_WithNonPositiveTtl_ThrowsArgumentOutOfRange()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _cache.SetAsync("event:any", new EventResponse { Title = "x" }, TimeSpan.Zero));
    }

    public void Dispose() => _redis.Dispose();
}