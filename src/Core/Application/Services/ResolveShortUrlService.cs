using Core.Application.DTOs;
using Core.Application.Ports.In;
using Core.Application.Ports.Out;
using Core.Domain.Exceptions;
using Core.Domain.ValueObjects;

namespace Core.Application.Services;

public sealed class ResolveShortUrlService(
    ICacheStore cache,
    IShortUrlRepository repo,
    IClock clock,
    ShortUrlOptions options) : IResolveShortUrl
{
    private const string NegativeCacheMarker = "__NOT_FOUND__";

    public async Task<ResolveShortUrlResult> ExecuteAsync(ResolveShortUrlRequest request, CancellationToken ct = default)
    {
        var code = ShortCode.Create(request.Code);

        // Try cache first for the URL
        var cachedUrl = await cache.GetAsync(code.Value, ct);
        
        // Check for negative cache marker (indicates previously not found)
        if (cachedUrl == NegativeCacheMarker)
        {
            throw new NotFoundException("Short URL not found.");
        }
        
        if (!string.IsNullOrEmpty(cachedUrl))
        {
            // Cache hit - still need to record access for accurate analytics
            var shortUrl = await repo.GetByCodeAsync(code.Value, ct);
            if (shortUrl != null && !shortUrl.IsExpired(clock))
            {
                // Record access and update repository
                shortUrl.RecordAccess(clock);
                await repo.UpdateAsync(shortUrl, ct);
            }
            return new ResolveShortUrlResult(cachedUrl);
        }

        // Cache miss - get from repository
        var entity = await repo.GetByCodeAsync(code.Value, ct);
        if (entity == null)
        {
            // Store negative cache marker with short TTL to prevent repeated DB lookups
            await cache.SetAsync(code.Value, NegativeCacheMarker, TimeSpan.FromSeconds(options.NegativeCacheTtlSeconds), ct);
            throw new NotFoundException("Short URL not found.");
        }

        // Check if expired
        if (entity.IsExpired(clock))
        {
            // Don't cache expired links for long - use negative cache TTL
            await cache.SetAsync(code.Value, NegativeCacheMarker, TimeSpan.FromSeconds(options.NegativeCacheTtlSeconds), ct);
            throw new ExpiredException("Short URL has expired.");
        }

        // Record access (increment clicks, update last access)
        entity.RecordAccess(clock);
        await repo.UpdateAsync(entity, ct);

        // Cache for future requests (24 hour TTL)
        await cache.SetAsync(code.Value, entity.OriginalUrl.Value, TimeSpan.FromHours(24), ct);

        return new ResolveShortUrlResult(entity.OriginalUrl.Value);
    }
}