namespace Core.Application.Ports.Out;

public interface ICacheStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default);
}