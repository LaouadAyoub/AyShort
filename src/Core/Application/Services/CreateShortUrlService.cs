using Core.Application.DTOs;
using Core.Application.Ports.In;
using Core.Application.Ports.Out;
using Core.Domain.Entities;
using Core.Domain.Exceptions;
using Core.Domain.ValueObjects;

namespace Core.Application.Services;

public sealed class CreateShortUrlService(
    IShortUrlRepository repo,
    ICodeGenerator generator,
    IClock clock,
    ICacheStore cache,
    ShortUrlOptions options) : ICreateShortUrl
{
    public async Task<CreateShortUrlResult> ExecuteAsync(CreateShortUrlRequest request, CancellationToken ct = default)
    {
        var url = OriginalUrl.Create(request.Url);

        if (request.Expiration is { } exp)
        {
            var min = clock.UtcNow.AddMinutes(options.MinTtlMinutes);
            var max = clock.UtcNow.AddDays(options.MaxTtlDays);
            if (exp < min) throw new ValidationException($"Expiration must be at least {options.MinTtlMinutes} minute(s) from now.");
            if (exp > max) throw new ValidationException($"Expiration must be within {options.MaxTtlDays} day(s).");
        }

        ShortCode code;

        if (!string.IsNullOrWhiteSpace(request.Alias))
        {
            code = ShortCode.Create(request.Alias!);
            if (await repo.CodeExistsAsync(code.Value, ct))
                throw new ConflictException("Alias already in use.");
        }
        else
        {
            var attempts = 0;
            do
            {
                if (attempts++ > 10) throw new ConflictException("Unable to generate a unique code.");
                code = ShortCode.Create(generator.Generate(options.CodeLength));
            } while (await repo.CodeExistsAsync(code.Value, ct));
        }

        var entity = ShortUrl.Create(code, url, request.Expiration, clock);
        await repo.AddAsync(entity, ct);

        // Warm cache after successful creation
        await cache.SetAsync(code.Value, entity.OriginalUrl.Value, TimeSpan.FromHours(24), ct);

        var baseUrl = options.BaseUrl.TrimEnd('/');
        var shortUrl = $"{baseUrl}/{code.Value}";
        return new CreateShortUrlResult(code.Value, shortUrl);
    }
}
