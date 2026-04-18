using Atlas.Connectors.Core.Caching;
using Atlas.Connectors.Core.Security;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Connectors.Core.DependencyInjection;

public static class ConnectorsCoreServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Connectors.Core 提供的默认实现：token 缓存 / OAuth state / replay guard，以及 ConnectorRegistry。
    /// Token 缓存优先级：若 DI 中已注册 HybridCache（生产 Redis 场景）→ 使用 HybridConnectorTokenCache；
    /// 否则降级到 InMemoryConnectorTokenCache（开发环境）。
    /// 调用方仍需通过 AddSingleton<IExternalIdentityProvider, XxxProvider>() 之类的方式注入各 provider 实现。
    /// </summary>
    public static IServiceCollection AddConnectorsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMemoryCache();
        services.TryAddSingleton<IConnectorTokenCache>(sp =>
        {
            // 当宿主应用调用 AddHybridCache() 后，DI 容器里就会有 HybridCache 实例；本适配会自动启用 L1+L2。
            var hybrid = sp.GetService<HybridCache>();
            if (hybrid is not null)
            {
                return new HybridConnectorTokenCache(hybrid);
            }
            return new InMemoryConnectorTokenCache(sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>());
        });
        services.TryAddSingleton<IOAuthStateStore, InMemoryOAuthStateStore>();
        services.TryAddSingleton<IReplayGuard, InMemoryReplayGuard>();
        services.TryAddSingleton<IConnectorRegistry, ConnectorRegistry>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
