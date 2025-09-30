using Core.Application;
using Core.Application.DTOs;
using Core.Application.Ports.Out;
using Core.Application.Services;
using Core.Domain.Entities;
using Core.Domain.Exceptions;
using Xunit;

file sealed class FakeClock : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2025-01-01T00:00:00Z"); }
file sealed class FakeRepo : IShortUrlRepository
{
    private readonly HashSet<string> _codes = new();
    private readonly Dictionary<string, ShortUrl> _links = new();
    
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) => Task.FromResult(_codes.Contains(code));
    
    public Task AddAsync(ShortUrl link, CancellationToken ct = default) 
    { 
        _codes.Add(link.Code.Value); 
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
file sealed class FakeGen : ICodeGenerator { public string Generate(int length = 7) => new('a', length); }

public class CreateShortUrlServiceTests
{
    [Fact]
    public async Task Creates_with_generated_code()
    {
        var svc = new CreateShortUrlService(new FakeRepo(), new FakeGen(), new FakeClock(), new ShortUrlOptions{ BaseUrl="http://x" });
        var res = await svc.ExecuteAsync(new CreateShortUrlRequest("https://example.com"));
        Assert.Equal("aaaaaaa", res.Code);
        Assert.Equal("http://x/aaaaaaa", res.ShortUrl);
    }

    [Fact]
    public async Task Rejects_invalid_scheme()
    {
        var svc = new CreateShortUrlService(new FakeRepo(), new FakeGen(), new FakeClock(), new ShortUrlOptions());
        await Assert.ThrowsAsync<ValidationException>(() => svc.ExecuteAsync(new CreateShortUrlRequest("ftp://x")));
    }
}
