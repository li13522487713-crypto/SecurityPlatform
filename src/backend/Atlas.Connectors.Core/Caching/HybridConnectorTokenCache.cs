using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;

namespace Atlas.Connectors.Core.Caching;

/// <summary>
/// 基于 <see cref="HybridCache"/>（.NET 10 内置 L1+L2 统一缓存）的 token 缓存适配。
/// L1=内存，L2=分布式（Redis 等）。生产环境注册 HybridCache 后自动启用此实现。
///
/// 注意：HybridCache 默认按 byte[] 序列化，需要传入可 JSON 序列化的类型；token cache 的载体记录均符合此约束。
/// </summary>
public sealed class HybridConnectorTokenCache : IConnectorTokenCache
{
    private readonly HybridCache _cache;

    public HybridConnectorTokenCache(HybridCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<TToken?> GetAsync<TToken>(string cacheKey, CancellationToken cancellationToken) where TToken : class
    {
        // HybridCache 没有「只读不刷新」语义；模拟方式：用一个会抛 KeyNotFoundException 的 factory，命中缓存就返回，否则吞异常返回 null。
        try
        {
            return await _cache.GetOrCreateAsync<TToken?>(
                cacheKey,
                static _ => ValueTask.FromException<TToken?>(new KeyNotFoundException("hybrid-cache-miss")),
                options: new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableLocalCacheWrite | HybridCacheEntryFlags.DisableDistributedCacheWrite },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    public async Task SetAsync<TToken>(string cacheKey, TToken token, TimeSpan ttl, CancellationToken cancellationToken) where TToken : class
    {
        var options = new HybridCacheEntryOptions { Expiration = ttl };
        await _cache.SetAsync(cacheKey, token, options, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string cacheKey, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TToken> GetOrCreateAsync<TToken>(string cacheKey, Func<CancellationToken, Task<(TToken Token, TimeSpan Ttl)>> factory, CancellationToken cancellationToken) where TToken : class
    {
        // HybridCache.GetOrCreateAsync 内部已对同 key 并发去重，与 InMemoryConnectorTokenCache 行为对齐。
        var holder = new FactoryHolder<TToken>(factory);

        // ttl 由 factory 返回；为了让 HybridCache 拿到正确 TTL，我们采用 set+get 的两段式：先用一个 placeholder TTL 做 GetOrCreate，
        // factory 内部把真实 TTL 通过外部回写。这里用闭包捕获 ttl 后再单独 SetAsync 强制刷新（同一进程内开销可忽略）。
        TimeSpan resolvedTtl = TimeSpan.FromSeconds(60);
        var token = await _cache.GetOrCreateAsync(
            cacheKey,
            holder,
            static async (state, ct) =>
            {
                var (t, ttl) = await state.Factory(ct).ConfigureAwait(false);
                state.ResolvedTtl = ttl;
                return t;
            },
            options: new HybridCacheEntryOptions { Expiration = resolvedTtl },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (holder.ResolvedTtl > TimeSpan.Zero && holder.ResolvedTtl != resolvedTtl)
        {
            // 用真实 TTL 覆盖默认 60s 占位。
            await _cache.SetAsync(cacheKey, token, new HybridCacheEntryOptions { Expiration = holder.ResolvedTtl }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        return token;
    }

    private sealed class FactoryHolder<TToken> where TToken : class
    {
        public FactoryHolder(Func<CancellationToken, Task<(TToken Token, TimeSpan Ttl)>> factory)
        {
            Factory = factory;
        }

        public Func<CancellationToken, Task<(TToken Token, TimeSpan Ttl)>> Factory { get; }

        public TimeSpan ResolvedTtl { get; set; } = TimeSpan.Zero;
    }
}
