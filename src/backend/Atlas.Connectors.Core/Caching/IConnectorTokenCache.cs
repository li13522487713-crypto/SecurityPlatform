namespace Atlas.Connectors.Core.Caching;

/// <summary>
/// 连接层统一的 token 缓存抽象。生产环境通常由 HybridCache 适配实现，开发/测试可替换为内存实现。
/// </summary>
public interface IConnectorTokenCache
{
    Task<TToken?> GetAsync<TToken>(string cacheKey, CancellationToken cancellationToken) where TToken : class;

    Task SetAsync<TToken>(string cacheKey, TToken token, TimeSpan ttl, CancellationToken cancellationToken) where TToken : class;

    Task RemoveAsync(string cacheKey, CancellationToken cancellationToken);

    /// <summary>
    /// 获取或刷新：缓存命中直接返回；缺失时调用 factory 获取并写入缓存（factory 内部应处理 ttl 略小于 token 实际 expires_in）。
    /// 同一 cacheKey 上的并发请求由实现保证只调用一次 factory（避免 token 风暴）。
    /// </summary>
    Task<TToken> GetOrCreateAsync<TToken>(string cacheKey, Func<CancellationToken, Task<(TToken Token, TimeSpan Ttl)>> factory, CancellationToken cancellationToken) where TToken : class;
}
