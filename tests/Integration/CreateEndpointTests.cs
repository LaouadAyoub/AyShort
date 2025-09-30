using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Core.Application.DTOs;
using Xunit;

public class CreateEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public CreateEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

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
}
