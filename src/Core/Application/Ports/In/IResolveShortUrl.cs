using Core.Application.DTOs;

namespace Core.Application.Ports.In;

public interface IResolveShortUrl
{
    Task<ResolveShortUrlResult> ExecuteAsync(ResolveShortUrlRequest request, CancellationToken ct = default);
}