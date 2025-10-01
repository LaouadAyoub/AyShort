using Core.Application;
using Core.Application.DTOs;
using Core.Application.Ports.Out;
using Core.Application.Services;
using Core.Domain.Entities;
using Core.Domain.Exceptions;
using Xunit;

file sealed class FakeClock : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2025-01-01T00:00:00Z"); }
file sealed class FakeCache : ICacheStore
{
    private readonly Dictionary<string, string> _cache = new();
    public Task<string?> GetAsync(string key, CancellationToken ct = default) => Task.FromResult(_cache.GetValueOrDefault(key));
    public Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default) { _cache[key] = value; return Task.CompletedTask; }
}

file sealed class FakeRepo : IShortUrlRepository
{
    private readonly Dictionary<string, ShortUrl> _links = new();
    
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) => Task.FromResult(_links.ContainsKey(code));
    
    public Task AddAsync(ShortUrl link, CancellationToken ct = default) 
    { 
        _links[link.Code.Value] = link;
        return Task.CompletedTask; 
    }
    
    public Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        _links.TryGetValue(code, out var link);
        return Task.FromResult(link);
    }
    
    public Task UpdateAsync(ShortUrl link, CancellationToken ct = default)
    {
        _links[link.Code.Value] = link;
        return Task.CompletedTask;
    }
}

public class ResolveShortUrlServiceTests
{
    [Fact]
    public async Task Cache_hit_returns_original_url()
    {
        var cache = new FakeCache();
        await cache.SetAsync("abc123", "https://example.com");
        
        var svc = new ResolveShortUrlService(cache, new FakeRepo(), new FakeClock());
        var result = await svc.ExecuteAsync(new ResolveShortUrlRequest("abc123"));
        
        Assert.Equal("https://example.com", result.OriginalUrl);
    }

    [Fact]
    public async Task Cache_miss_gets_from_repo_and_caches()
    {
        var cache = new FakeCache();
        var repo = new FakeRepo();
        var clock = new FakeClock();
        
        // Setup: add link to repo but not cache
        var shortUrl = ShortUrl.Create(
            Core.Domain.ValueObjects.ShortCode.Create("abc123"), 
            Core.Domain.ValueObjects.OriginalUrl.Create("https://example.com"),
            null, 
            clock);
        await repo.AddAsync(shortUrl);
        
        var svc = new ResolveShortUrlService(cache, repo, clock);
        var result = await svc.ExecuteAsync(new ResolveShortUrlRequest("abc123"));
        
        Assert.Equal("https://example.com/", result.OriginalUrl);
        // Should be cached now
        var cached = await cache.GetAsync("abc123");
        Assert.Equal("https://example.com/", cached);
    }

    [Fact]
    public async Task Unknown_code_throws_not_found()
    {
        var svc = new ResolveShortUrlService(new FakeCache(), new FakeRepo(), new FakeClock());
        await Assert.ThrowsAsync<NotFoundException>(() => 
            svc.ExecuteAsync(new ResolveShortUrlRequest("unknown")));
    }

    [Fact]
    public async Task Expired_link_throws_expired()
    {
        var cache = new FakeCache();
        var repo = new FakeRepo();
        var clock = new FakeClock { UtcNow = DateTimeOffset.Parse("2025-01-01T00:00:00Z") };
        
        // Create link that will expire soon, then advance time
        var shortUrl = ShortUrl.Create(
            Core.Domain.ValueObjects.ShortCode.Create("expired"), 
            Core.Domain.ValueObjects.OriginalUrl.Create("https://example.com"),
            DateTimeOffset.Parse("2025-01-02T00:00:00Z"), // expires tomorrow
            clock);
        await repo.AddAsync(shortUrl);
        
        // Advance time past expiration
        clock.UtcNow = DateTimeOffset.Parse("2025-01-03T00:00:00Z");
        
        var svc = new ResolveShortUrlService(cache, repo, clock);
        await Assert.ThrowsAsync<ExpiredException>(() => 
            svc.ExecuteAsync(new ResolveShortUrlRequest("expired")));
    }

    [Fact]
    public async Task Records_access_increments_clicks()
    {
        var cache = new FakeCache();
        var repo = new FakeRepo();
        var clock = new FakeClock();
        
        var shortUrl = ShortUrl.Create(
            Core.Domain.ValueObjects.ShortCode.Create("test"), 
            Core.Domain.ValueObjects.OriginalUrl.Create("https://example.com"),
            null, 
            clock);
        await repo.AddAsync(shortUrl);
        
        var svc = new ResolveShortUrlService(cache, repo, clock);
        await svc.ExecuteAsync(new ResolveShortUrlRequest("test"));
        
        var updated = await repo.GetByCodeAsync("test");
        Assert.NotNull(updated);
        Assert.Equal(1, updated!.ClicksCount);
        Assert.Equal(clock.UtcNow, updated.LastAccessAt);
    }
}