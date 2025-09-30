using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Core.Application.DTOs;
using Xunit;

public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ApiEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Post_links_returns_201()
    {
        var client = _factory.CreateClient();
        var payload = new CreateShortUrlRequest("https://example.com/path");
        var resp = await client.PostAsJsonAsync("/links", payload);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<CreateShortUrlResult>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.Code);
        Assert.Contains(result.Code, result.ShortUrl);
    }

    [Fact]
    public async Task Post_links_invalid_scheme_returns_400()
    {
        var client = _factory.CreateClient();
        var payload = new { url = "ftp://invalid" };
        var resp = await client.PostAsJsonAsync("/links", payload);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Get_code_redirects_to_original_url()
    {
        // Create client that doesn't follow redirects
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        
        // First create a short URL
        var createPayload = new CreateShortUrlRequest("https://example.com/redirect-test");
        var createResp = await client.PostAsJsonAsync("/links", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createResult = await createResp.Content.ReadFromJsonAsync<CreateShortUrlResult>();
        Assert.NotNull(createResult);
        Assert.NotEmpty(createResult.Code);
        

        
        // Check if we can retrieve it immediately after creation using the same client
        var resolveResp = await client.GetAsync($"/{createResult!.Code}");
        Assert.Equal(HttpStatusCode.Found, resolveResp.StatusCode);
        Assert.Equal("https://example.com/redirect-test", resolveResp.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Get_unknown_code_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/unknown123");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Post_links_with_expiration_succeeds()
    {
        // Create client that doesn't follow redirects
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        
        // Create a short URL with minimum valid expiration (1 minute + buffer)
        var createPayload = new CreateShortUrlRequest(
            "https://example.com/expire-test", 
            null, 
            DateTimeOffset.UtcNow.AddMinutes(1).AddSeconds(2)); // expires in ~62 seconds
        var createResp = await client.PostAsJsonAsync("/links", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createResult = await createResp.Content.ReadFromJsonAsync<CreateShortUrlResult>();
        Assert.NotNull(createResult);
        
        // Verify the created URL can be resolved immediately (not expired yet)
        var resolveResp = await client.GetAsync($"/{createResult!.Code}");
        Assert.Equal(HttpStatusCode.Found, resolveResp.StatusCode);
        Assert.Equal("https://example.com/expire-test", resolveResp.Headers.Location?.ToString());
        
        // NOTE: Testing actual expiration (410 Gone) would require mocking IClock
        // to simulate time passage without waiting in real time
    }
}
