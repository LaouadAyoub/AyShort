using Core.Application.DTOs;

namespace Core.Application.Ports.In;

public interface ICreateShortUrl
{
    Task<CreateShortUrlResult> ExecuteAsync(CreateShortUrlRequest request, CancellationToken ct = default);
}
