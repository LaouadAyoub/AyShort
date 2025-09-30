using Core.Domain.Entities;

namespace Core.Application.Ports.Out;

public interface IShortUrlRepository
{
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    Task AddAsync(ShortUrl link, CancellationToken ct = default);
    Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task UpdateAsync(ShortUrl link, CancellationToken ct = default);
}
