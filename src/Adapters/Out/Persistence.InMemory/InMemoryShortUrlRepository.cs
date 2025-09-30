using System.Collections.Concurrent;
using Core.Application.Ports.Out;
using Core.Domain.Entities;

namespace Adapters.Out.Persistence.InMemory;

public sealed class InMemoryShortUrlRepository : IShortUrlRepository
{
    private readonly ConcurrentDictionary<string, ShortUrl> _store = new(StringComparer.Ordinal);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
        => Task.FromResult(_store.ContainsKey(code));

    public Task AddAsync(ShortUrl link, CancellationToken ct = default)
    {
        _store.TryAdd(link.Code.Value, link);
        return Task.CompletedTask;
    }
}
