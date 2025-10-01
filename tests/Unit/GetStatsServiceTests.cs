using Core.Application.DTOs;
using Core.Application.Ports.Out;
using Core.Application.Services;
using Core.Domain.Entities;
using Core.Domain.Exceptions;
using Core.Domain.ValueObjects;
using Xunit;

file sealed class FakeClock : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2025-01-01T00:00:00Z"); }
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

public class GetStatsServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ExistingCode_ReturnsStats()
    {
        // Arrange
        var clock = new FakeClock();
        var repo = new FakeRepo();
        var service = new GetStatsService(repo);
        
        var code = ShortCode.Create("abc123");
        var url = OriginalUrl.Create("https://example.com");
        var expiration = DateTimeOffset.Parse("2025-12-31T23:59:59Z");
        var entity = ShortUrl.Create(code, url, expiration, clock);
        
        // Simulate some clicks
        entity.RecordAccess(clock);
        clock.UtcNow = DateTimeOffset.Parse("2025-01-01T12:00:00Z");
        entity.RecordAccess(clock);
        clock.UtcNow = DateTimeOffset.Parse("2025-01-01T18:30:00Z");
        entity.RecordAccess(clock);
        
        await repo.AddAsync(entity);
        
        var request = new GetStatsRequest("abc123");

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(DateTimeOffset.Parse("2025-01-01T00:00:00Z"), result.CreatedAt);
        Assert.Equal(3, result.Clicks);
        Assert.Equal(DateTimeOffset.Parse("2025-01-01T18:30:00Z"), result.LastAccess);
        Assert.Equal(expiration, result.Expiration);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingCodeNoClicks_ReturnsStatsWithZeroClicks()
    {
        // Arrange
        var clock = new FakeClock();
        var repo = new FakeRepo();
        var service = new GetStatsService(repo);
        
        var code = ShortCode.Create("xyz789");
        var url = OriginalUrl.Create("https://example.com");
        var entity = ShortUrl.Create(code, url, null, clock); // No expiration
        
        await repo.AddAsync(entity);
        
        var request = new GetStatsRequest("xyz789");

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(DateTimeOffset.Parse("2025-01-01T00:00:00Z"), result.CreatedAt);
        Assert.Equal(0, result.Clicks);
        Assert.Null(result.LastAccess);
        Assert.Null(result.Expiration);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentCode_ThrowsNotFoundException()
    {
        // Arrange
        var repo = new FakeRepo();
        var service = new GetStatsService(repo);
        var request = new GetStatsRequest("notfound");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => service.ExecuteAsync(request));
        Assert.Equal("Short URL with code 'notfound' not found.", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredUrl_StillReturnsStats()
    {
        // Arrange
        var clock = new FakeClock();
        var repo = new FakeRepo();
        var service = new GetStatsService(repo);
        
        var code = ShortCode.Create("expired");
        var url = OriginalUrl.Create("https://example.com");
        var expiration = DateTimeOffset.Parse("2025-12-31T23:59:59Z"); // Future when created
        var entity = ShortUrl.Create(code, url, expiration, clock);
        
        // Record some access before expiration
        entity.RecordAccess(clock);
        
        await repo.AddAsync(entity);
        
        // Fast forward time to after expiration (clock is used only for validation during create)
        clock.UtcNow = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        
        var request = new GetStatsRequest("expired");

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert - Stats should still be returned even for expired URLs
        Assert.Equal(DateTimeOffset.Parse("2025-01-01T00:00:00Z"), result.CreatedAt);
        Assert.Equal(1, result.Clicks);
        Assert.Equal(DateTimeOffset.Parse("2025-01-01T00:00:00Z"), result.LastAccess);
        Assert.Equal(expiration, result.Expiration);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidCode_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeRepo();
        var service = new GetStatsService(repo);
        var request = new GetStatsRequest(""); // Invalid empty code

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.ExecuteAsync(request));
    }
}