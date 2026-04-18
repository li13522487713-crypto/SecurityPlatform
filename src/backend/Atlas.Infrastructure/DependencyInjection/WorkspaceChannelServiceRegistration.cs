using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Infrastructure.Services.AiPlatform.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Infrastructure.DependencyInjection;

/// <summary>
/// 工作空间渠道发布基础设施 DI（M-G02）。
/// 仅注册渠道注册中心；具体 IWorkspaceChannelConnector 实现（Web SDK / Open API / 飞书 / 微信公众号）
/// 由各自 case 在专属注册扩展中按需 AddSingleton&lt;IWorkspaceChannelConnector, XxxConnector&gt;()。
/// 使用 TryAdd 以容忍多入口（AppHost / PlatformHost）重复调用。
/// </summary>
public static class WorkspaceChannelServiceRegistration
{
    public static IServiceCollection AddWorkspaceChannelInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton<IWorkspaceChannelConnectorRegistry, WorkspaceChannelConnectorRegistry>();
        return services;
    }
}
