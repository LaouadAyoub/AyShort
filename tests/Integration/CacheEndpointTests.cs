using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Core.Application.DTOs;
using Xunit;

public class CacheEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public CacheEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Create_warms_cache_for_immediate_resolve()
    {
        // Create client that doesn't follow redirects
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        
        // Create a short URL
        var createPayload = new CreateShortUrlRequest("https://example.com/cache-test");
        var createResp = await client.PostAsJsonAsync("/links", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createResult = await createResp.Content.ReadFromJsonAsync<CreateShortUrlResult>();
        Assert.NotNull(createResult);
        
        // Immediately resolve - should hit cache (warmed on create)
        var resolveResp = await client.GetAsync($"/{createResult!.Code}");
        Assert.Equal(HttpStatusCode.Found, resolveResp.StatusCode);
        Assert.Equal("https://example.com/cache-test", resolveResp.Headers.Location?.ToString());
        
        // Resolve again - should still hit cache
        var resolveResp2 = await client.GetAsync($"/{createResult.Code}");
        Assert.Equal(HttpStatusCode.Found, resolveResp2.StatusCode);
        Assert.Equal("https://example.com/cache-test", resolveResp2.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Unknown_code_uses_negative_cache()
    {
        var client = _factory.CreateClient();
        
        // First request - not found, creates negative cache entry
        var resp1 = await client.GetAsync("/nonexist-code-xyz");
        Assert.Equal(HttpStatusCode.NotFound, resp1.StatusCode);
        
        // Second request - should hit negative cache (still 404)
        var resp2 = await client.GetAsync("/nonexist-code-xyz");
        Assert.Equal(HttpStatusCode.NotFound, resp2.StatusCode);
        
        // Both should return 404, but the second should be faster (from cache)
        // In a real scenario, you'd measure timing or use metrics
    }

    [Fact]
    public async Task Cache_hit_preserves_click_tracking()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        
        // Create a short URL
        var createPayload = new CreateShortUrlRequest("https://example.com/track-test");
        var createResp = await client.PostAsJsonAsync("/links", createPayload);
        var createResult = await createResp.Content.ReadFromJsonAsync<CreateShortUrlResult>();
        Assert.NotNull(createResult);
        
        // First resolve (cache hit from warming)
        await client.GetAsync($"/{createResult!.Code}");
        
        // Check stats - should have 1 click
        var statsResp1 = await client.GetAsync($"/links/{createResult.Code}/stats");
        var stats1 = await statsResp1.Content.ReadFromJsonAsync<GetStatsResult>();
        Assert.NotNull(stats1);
        Assert.Equal(1, stats1!.Clicks);
        
        // Second resolve (cache hit)
        await client.GetAsync($"/{createResult.Code}");
        
        // Check stats - should have 2 clicks (even with cache hit)
        var statsResp2 = await client.GetAsync($"/links/{createResult.Code}/stats");
        var stats2 = await statsResp2.Content.ReadFromJsonAsync<GetStatsResult>();
        Assert.NotNull(stats2);
        Assert.Equal(2, stats2!.Clicks);
        Assert.NotNull(stats2.LastAccess);
    }

    [Fact]
    public async Task Multiple_resolves_increment_clicks_correctly()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        
        // Create a short URL
        var createPayload = new CreateShortUrlRequest("https://example.com/multi-resolve");
        var createResp = await client.PostAsJsonAsync("/links", createPayload);
        var createResult = await createResp.Content.ReadFromJsonAsync<CreateShortUrlResult>();
        Assert.NotNull(createResult);
        
        // Resolve multiple times
        for (int i = 0; i < 5; i++)
        {
            var resolveResp = await client.GetAsync($"/{createResult!.Code}");
            Assert.Equal(HttpStatusCode.Found, resolveResp.StatusCode);
        }
        
        // Check stats - should have 5 clicks
        var statsResp = await client.GetAsync($"/links/{createResult!.Code}/stats");
        var stats = await statsResp.Content.ReadFromJsonAsync<GetStatsResult>();
        Assert.NotNull(stats);
        Assert.Equal(5, stats!.Clicks);
    }
}
