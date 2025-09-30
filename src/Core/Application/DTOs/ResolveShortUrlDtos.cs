namespace Core.Application.DTOs;

public sealed record ResolveShortUrlRequest(string Code);
public sealed record ResolveShortUrlResult(string OriginalUrl);