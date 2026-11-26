using System.Collections.Concurrent;
using EventsService.Application.Abstractions;

namespace EventsService.IntegrationTests;

public sealed class InMemoryCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, object> _store = new();
    private readonly ConcurrentQueue<(string Key, TimeSpan Ttl)> _sets = new();
    private readonly ConcurrentQueue<string> _removed = new();

    public IReadOnlyList<(string Key, TimeSpan Ttl)> SetCalls => _sets.ToArray();
    public IReadOnlyList<string> RemovedKeys => _removed.ToArray();

    public bool Contains(string key) => _store.ContainsKey(key);

    public void Seed(string key, object value) => _store[key] = value;

    public void ClearCalls()
    {
        _sets.Clear();
        _removed.Clear();
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        => Task.FromResult<T?>(_store.TryGetValue(key, out var value) ? value as T : null);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        _store[key] = value;
        _sets.Enqueue((key, ttl));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        _removed.Enqueue(key);
        return Task.CompletedTask;
    }
}