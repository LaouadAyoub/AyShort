namespace Adapters.Out.Cache.Redis;

public sealed class RedisCacheOptions
{
    public string Connection { get; init; } = "localhost:6379";
    public int DefaultTtlSeconds { get; init; } = 86400; // 24 hours
    public int NegativeTtlSeconds { get; init; } = 60;   // 1 minute
    public string NegativeCacheMarker { get; init; } = "__NOT_FOUND__";
}
