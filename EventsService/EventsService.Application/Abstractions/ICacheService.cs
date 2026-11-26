namespace EventsService.Application.Abstractions;

public interface ICacheService
{
    /// <summary>Returns the cached value, or null on a miss or when the cache is unavailable.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Stores the value with a time-to-live. Failures are logged and never thrown.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Removes the key. Failures are logged and never thrown.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}