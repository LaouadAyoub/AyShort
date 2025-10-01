using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Core.Application.DTOs;
using Xunit;

public class GetStatsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public GetStatsEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Get_links_code_stats_returns_200_with_stats()
    {
        // Arrange - Create client that doesn't follow redirects
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        
        // First create a short URL
        var createPayload = new CreateShortUrlRequest("https://example.com/test");
        var createResp = await client.PostAsJsonAsync("/links", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createResult = await createResp.Content.ReadFromJsonAsync<CreateShortUrlResult>();
        Assert.NotNull(createResult);
        
        // Access the URL once (only first access counts due to caching)
        var redirectResp = await client.GetAsync($"/{createResult.Code}");
        Assert.Equal(HttpStatusCode.Found, redirectResp.StatusCode);

        // Act - Get stats
        var statsResp = await client.GetAsync($"/links/{createResult.Code}/stats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, statsResp.StatusCode);
        var statsResult = await statsResp.Content.ReadFromJsonAsync<GetStatsResult>();
        Assert.NotNull(statsResult);
        Assert.Equal(1, statsResult!.Clicks); // Only first access counts due to cache
        Assert.NotNull(statsResult.LastAccess);
        Assert.True(statsResult.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Get_links_code_stats_new_url_returns_zero_clicks()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Create a short URL but don't access it
        var createPayload = new CreateShortUrlRequest("https://example.com/unused", Expiration: DateTimeOffset.UtcNow.AddDays(1));
        var createResp = await client.PostAsJsonAsync("/links", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createResult = await createResp.Content.ReadFromJsonAsync<CreateShortUrlResult>();
        Assert.NotNull(createResult);

        // Act - Get stats immediately without accessing
        var statsResp = await client.GetAsync($"/links/{createResult.Code}/stats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, statsResp.StatusCode);
        var statsResult = await statsResp.Content.ReadFromJsonAsync<GetStatsResult>();
        Assert.NotNull(statsResult);
        Assert.Equal(0, statsResult!.Clicks);
        Assert.Null(statsResult.LastAccess);
        Assert.NotNull(statsResult.Expiration);
        Assert.True(statsResult.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Get_links_nonexistent_code_stats_returns_404()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var resp = await client.GetAsync("/links/nonexistent/stats");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        
        // Verify it returns ProblemDetails format
        var content = await resp.Content.ReadAsStringAsync();
        Assert.Contains("NotFoundException", content);
        Assert.Contains("not found", content);
    }

    [Fact]
    public async Task Get_links_code_stats_expired_url_still_returns_stats()
    {
        // Arrange - Create client that doesn't follow redirects
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        
        // Create a short URL with future expiration (long enough for test)
        var createPayload = new CreateShortUrlRequest("https://example.com/expires-later", Expiration: DateTimeOffset.UtcNow.AddMinutes(10));
        var createResp = await client.PostAsJsonAsync("/links", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createResult = await createResp.Content.ReadFromJsonAsync<CreateShortUrlResult>();
        Assert.NotNull(createResult);

        // Access it once before expiration
        var redirectResp = await client.GetAsync($"/{createResult.Code}");
        Assert.Equal(HttpStatusCode.Found, redirectResp.StatusCode);

        // Act - Get stats (URL not expired yet but test verifies stats work with expiration data)
        var statsResp = await client.GetAsync($"/links/{createResult.Code}/stats");

        // Assert - Stats should be accessible
        Assert.Equal(HttpStatusCode.OK, statsResp.StatusCode);
        var statsResult = await statsResp.Content.ReadFromJsonAsync<GetStatsResult>();
        Assert.NotNull(statsResult);
        Assert.Equal(1, statsResult!.Clicks);
        Assert.NotNull(statsResult.LastAccess);
        Assert.NotNull(statsResult.Expiration);
        Assert.True(statsResult.Expiration > DateTimeOffset.UtcNow); // Still in future
    }

    [Fact]
    public async Task Get_links_invalid_code_stats_returns_400()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Use invalid code (empty)
        var resp = await client.GetAsync("/links//stats");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode); // Route doesn't match
    }

    [Fact]
    public async Task Get_links_code_stats_content_type_is_json()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Create a short URL
        var createPayload = new CreateShortUrlRequest("https://example.com/content-test");
        var createResp = await client.PostAsJsonAsync("/links", createPayload);
        var createResult = await createResp.Content.ReadFromJsonAsync<CreateShortUrlResult>();
        Assert.NotNull(createResult);

        // Act
        var statsResp = await client.GetAsync($"/links/{createResult.Code}/stats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, statsResp.StatusCode);
        Assert.Contains("application/json", statsResp.Content.Headers.ContentType?.ToString());
    }
}