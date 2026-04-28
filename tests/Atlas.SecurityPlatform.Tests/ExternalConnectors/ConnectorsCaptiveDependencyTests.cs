using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.DependencyInjection;
using Atlas.Connectors.DingTalk;
using Atlas.Connectors.Feishu;
using Atlas.Connectors.WeCom;
using Atlas.Infrastructure.ExternalConnectors.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.ExternalConnectors;

/// <summary>
/// 回归用例：保证 Connectors.WeCom / Feishu / DingTalk 三库的 Singleton 服务（ApiClient + 4 个 Provider）
/// 在启用 ValidateScopes=true 的容器中可以直接从根作用域解析，不会因为捕获 Scoped 服务而抛
/// InvalidOperationException（即不会再触发 Microsoft.Extensions.DependencyInjection 的 captive dependency 检查）。
///
/// 真实环境下 IConnectorRuntimeOptionsAccessor 会在 Scope 内由调用方调用并把结果塞入 ConnectorContext.RuntimeOptions，
/// Singleton 底层库不再注入 IConnectorRuntimeOptionsResolver/Accessor，因此 ServiceProvider 校验通过即代表 captive dep 修复成功。
/// </summary>
public sealed class ConnectorsCaptiveDependencyTests
{
    [Fact]
    public void ConnectorSingletons_ResolveCleanly_UnderValidateScopes()
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);
        services.AddLogging();
        services.AddOptions<WeComOptions>();
        services.AddOptions<FeishuOptions>();
        services.AddOptions<DingTalkOptions>();
        // 注册 IConnectorTokenCache / IConnectorRegistry 等 Connectors.Core 默认实现。
        services.AddConnectorsCore();
        // Resolver 端口由 Application 层定义；Connectors.* Singleton 不再依赖它们 → 这里给 NSubstitute 占位即可。
        services.AddScoped(_ => Substitute.For<IConnectorRuntimeOptionsResolver<WeComRuntimeOptions>>());
        services.AddScoped(_ => Substitute.For<IConnectorRuntimeOptionsResolver<FeishuRuntimeOptions>>());
        services.AddScoped(_ => Substitute.For<IConnectorRuntimeOptionsResolver<DingTalkRuntimeOptions>>());

        services.AddWeComConnector();
        services.AddFeishuConnector();
        services.AddDingTalkConnector();

        // 关键开关：ValidateScopes=true（与 AppHost Development 默认行为一致）。
        // 同时打开 ValidateOnBuild，让任何 captive dep 在 Build 时直接暴露。
        using var sp = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

        // 直接从根作用域解析 Singleton ApiClient + 4 大 Provider，必须不抛。
        Assert.NotNull(sp.GetRequiredService<WeComApiClient>());
        Assert.NotNull(sp.GetRequiredService<FeishuApiClient>());
        Assert.NotNull(sp.GetRequiredService<DingTalkApiClient>());

        var identityProviders = sp.GetServices<IExternalIdentityProvider>().ToList();
        Assert.Contains(identityProviders, p => p.ProviderType == WeComConnectorMarker.ProviderType);
        Assert.Contains(identityProviders, p => p.ProviderType == FeishuConnectorMarker.ProviderType);
        Assert.Contains(identityProviders, p => p.ProviderType == DingTalkConnectorMarker.ProviderType);

        Assert.Equal(3, sp.GetServices<IExternalDirectoryProvider>().Count());
        Assert.Equal(3, sp.GetServices<IExternalApprovalProvider>().Count());
        Assert.Equal(3, sp.GetServices<IExternalMessagingProvider>().Count());
        Assert.Equal(3, sp.GetServices<IConnectorEventVerifier>().Count());
    }
}
