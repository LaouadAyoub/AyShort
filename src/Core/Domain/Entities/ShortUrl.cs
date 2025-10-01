using Core.Application.Ports.Out;
using Core.Domain.Exceptions;
using Core.Domain.ValueObjects;

namespace Core.Domain.Entities;

public sealed class ShortUrl
{
    public ShortCode Code { get; }
    public OriginalUrl OriginalUrl { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? Expiration { get; }

    public int ClicksCount { get; private set; }
    public DateTimeOffset? LastAccessAt { get; private set; }

    private ShortUrl(ShortCode code, OriginalUrl url, DateTimeOffset createdAt, DateTimeOffset? expiration)
    {
        Code = code;
        OriginalUrl = url;
        CreatedAt = createdAt;
        Expiration = expiration;
    }

    public static ShortUrl Create(ShortCode code, OriginalUrl url, DateTimeOffset? expiration, IClock clock)
    {
        DateTimeOffset now = clock.UtcNow;
        if (expiration.HasValue && expiration.Value <= now)
            throw new ValidationException("Expiration must be in the future.");

        return new ShortUrl(code, url, now, expiration);
    }

    public void RecordAccess(IClock clock)
    {
        ClicksCount++;
        LastAccessAt = clock.UtcNow;
    }

    public bool IsExpired(IClock clock) => Expiration.HasValue && clock.UtcNow > Expiration.Value;
}
