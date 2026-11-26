using System.Text.Json;
using EventsService.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EventsService.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            if (!_redis.IsConnected)
            {
                _logger.LogWarning("Redis is not connected: cache read skipped for key {Key}.", key);
                return null;
            }

            var value = await _redis.GetDatabase().StringGetAsync(key).WaitAsync(cancellationToken);
            if (value.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Cache read failed for key {Key}; falling back to the database.", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);

        try
        {
            if (!_redis.IsConnected)
            {
                _logger.LogWarning("Redis is not connected: cache write skipped for key {Key}.", key);
                return;
            }

            var json = JsonSerializer.Serialize(value, JsonOptions);
            await _redis.GetDatabase().StringSetAsync(key, json, ttl).WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Cache write failed for key {Key}.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
            {
                _logger.LogError("Redis is not connected: key {Key} was not removed and may stay stale until its TTL expires.", key);
                return;
            }

            await _redis.GetDatabase().KeyDeleteAsync(key).WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Cache removal failed for key {Key}; the entry may stay stale until its TTL expires.", key);
        }
    }
}