using Core.Application.DTOs;

namespace Core.Application.Ports.In;

public interface IGetStats
{
    Task<GetStatsResult> ExecuteAsync(GetStatsRequest request, CancellationToken ct = default);
}