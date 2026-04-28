using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Atlas.Connectors.Core.Caching;

/// <summary>
/// 基于 <see cref="IMemoryCache"/> 的默认实现。生产环境若有 Redis，应换成 HybridCache 适配实现。
/// 同一 cacheKey 的并发刷新使用 SemaphoreSlim 防风暴。
/// </summary>
public sealed class InMemoryConnectorTokenCache : IConnectorTokenCache, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public InMemoryConnectorTokenCache(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public Task<TToken?> GetAsync<TToken>(string cacheKey, CancellationToken cancellationToken) where TToken : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        _cache.TryGetValue(cacheKey, out var value);
        return Task.FromResult(value as TToken);
    }

    public Task SetAsync<TToken>(string cacheKey, TToken token, TimeSpan ttl, CancellationToken cancellationToken) where TToken : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        _cache.Set(cacheKey, token, ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _cache.Remove(cacheKey);
        return Task.CompletedTask;
    }

    public async Task<TToken> GetOrCreateAsync<TToken>(string cacheKey, Func<CancellationToken, Task<(TToken Token, TimeSpan Ttl)>> factory, CancellationToken cancellationToken) where TToken : class
    {
        if (_cache.TryGetValue<TToken>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue<TToken>(cacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            var (token, ttl) = await factory(cancellationToken).ConfigureAwait(false);
            _cache.Set(cacheKey, token, ttl);
            return token;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        foreach (var gate in _locks.Values)
        {
            gate.Dispose();
        }
        _locks.Clear();
    }
}
