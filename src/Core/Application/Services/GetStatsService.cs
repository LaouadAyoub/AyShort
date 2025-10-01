using Core.Application.DTOs;
using Core.Application.Ports.In;
using Core.Application.Ports.Out;
using Core.Domain.Exceptions;
using Core.Domain.ValueObjects;

namespace Core.Application.Services;

public sealed class GetStatsService(IShortUrlRepository repo) : IGetStats
{
    public async Task<GetStatsResult> ExecuteAsync(GetStatsRequest request, CancellationToken ct = default)
    {
        var code = ShortCode.Create(request.Code);
        var entity = await repo.GetByCodeAsync(code.Value, ct);
        
        if (entity is null)
            throw new NotFoundException($"Short URL with code '{request.Code}' not found.");

        return new GetStatsResult(
            entity.CreatedAt,
            entity.ClicksCount,
            entity.LastAccessAt,
            entity.Expiration);
    }
}