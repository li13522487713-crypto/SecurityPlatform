using Atlas.Connectors.Core.Caching;
using Atlas.Connectors.Core.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Connectors.Core.DependencyInjection;

public static class ConnectorsCoreServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Connectors.Core 提供的默认实现：进程内 token 缓存 / OAuth state / replay guard，以及 ConnectorRegistry。
    /// 调用方仍需通过 AddSingleton<IExternalIdentityProvider, XxxProvider>() 之类的方式注入各 provider 实现。
    /// </summary>
    public static IServiceCollection AddConnectorsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMemoryCache();
        services.TryAddSingleton<IConnectorTokenCache, InMemoryConnectorTokenCache>();
        services.TryAddSingleton<IOAuthStateStore, InMemoryOAuthStateStore>();
        services.TryAddSingleton<IReplayGuard, InMemoryReplayGuard>();
        services.TryAddSingleton<IConnectorRegistry, ConnectorRegistry>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
