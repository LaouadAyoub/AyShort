using Core.Application.DTOs;
using Core.Application.Ports.In;
using Core.Application.Ports.Out;
using Core.Domain.Exceptions;
using Core.Domain.ValueObjects;

namespace Core.Application.Services;

public sealed class ResolveShortUrlService(
    ICacheStore cache,
    IShortUrlRepository repo,
    IClock clock) : IResolveShortUrl
{
    public async Task<ResolveShortUrlResult> ExecuteAsync(ResolveShortUrlRequest request, CancellationToken ct = default)
    {
        var code = ShortCode.Create(request.Code);

        // Try cache first
        var cachedUrl = await cache.GetAsync(code.Value, ct);
        if (!string.IsNullOrEmpty(cachedUrl))
        {
            return new ResolveShortUrlResult(cachedUrl);
        }

        // Cache miss - get from repository
        var shortUrl = await repo.GetByCodeAsync(code.Value, ct);
        if (shortUrl == null)
        {
            throw new NotFoundException("Short URL not found.");
        }

        // Check if expired
        if (shortUrl.IsExpired(clock))
        {
            throw new ExpiredException("Short URL has expired.");
        }

        // Record access (increment clicks, update last access)
        shortUrl.RecordAccess(clock);
        await repo.UpdateAsync(shortUrl, ct);

        // Cache for future requests (24 hour TTL)
        await cache.SetAsync(code.Value, shortUrl.OriginalUrl.Value, TimeSpan.FromHours(24), ct);

        return new ResolveShortUrlResult(shortUrl.OriginalUrl.Value);
    }
}