using Core.Application.Ports.Out;
using StackExchange.Redis;

namespace Adapters.Out.Cache.Redis;

public sealed class RedisCacheStore : ICacheStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisCacheOptions _options;

    public RedisCacheStore(IConnectionMultiplexer redis, RedisCacheOptions options)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            
            return value.HasValue ? value.ToString() : null;
        }
        catch (RedisException)
        {
            // Log error in production, but degrade gracefully
            // Return null to force fallback to database
            return null;
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var expiry = ttl ?? TimeSpan.FromSeconds(_options.DefaultTtlSeconds);
            await db.StringSetAsync(key, value, expiry);
        }
        catch (RedisException)
        {
            // Log error in production, but don't throw
            // Cache writes should not break the application
        }
    }
}
