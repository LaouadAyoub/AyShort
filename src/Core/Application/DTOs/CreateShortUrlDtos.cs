namespace Core.Application.DTOs;

public sealed record CreateShortUrlRequest(string Url, string? Alias = null, DateTimeOffset? Expiration = null);
public sealed record CreateShortUrlResult(string Code, string ShortUrl);
