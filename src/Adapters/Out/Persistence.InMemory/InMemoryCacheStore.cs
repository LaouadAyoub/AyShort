using System.Collections.Concurrent;
using Core.Application.Ports.Out;

namespace Adapters.Out.Persistence.InMemory;

public sealed class InMemoryCacheStore : ICacheStore
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt == null || DateTimeOffset.UtcNow < entry.ExpiresAt.Value)
            {
                return Task.FromResult<string?>(entry.Value);
            }
            // Expired - remove it
            _cache.TryRemove(key, out _);
        }
        return Task.FromResult<string?>(null);
    }

    public Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        DateTimeOffset? expiresAt = ttl.HasValue ? DateTimeOffset.UtcNow.Add(ttl.Value) : null;
        _cache.AddOrUpdate(key, new CacheEntry(value, expiresAt), (_, _) => new CacheEntry(value, expiresAt));
        return Task.CompletedTask;
    }

    private sealed record CacheEntry(string Value, DateTimeOffset? ExpiresAt);
}