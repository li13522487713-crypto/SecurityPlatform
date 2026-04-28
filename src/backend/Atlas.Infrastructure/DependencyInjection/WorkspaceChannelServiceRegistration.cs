using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Infrastructure.Services.AiPlatform.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Infrastructure.DependencyInjection;

/// <summary>
/// 工作空间渠道发布基础设施 DI（M-G02）。
/// 仅注册渠道注册中心；具体 IWorkspaceChannelConnector 实现（Web SDK / Open API / 飞书 / 微信公众号）
/// 由各自 case 在专属注册扩展中按需 AddSingleton&lt;IWorkspaceChannelConnector, XxxConnector&gt;()。
/// 使用 TryAdd 以容忍AppHost 内重复调用。
/// </summary>
public static class WorkspaceChannelServiceRegistration
{
    public static IServiceCollection AddWorkspaceChannelInfrastructure(this IServiceCollection services)
    {
        // registry 与 connectors 同为 Scoped，避免「Singleton 捕获 Scoped」的告警；
        // 真正需要跨请求共享的状态（如 OpenAPI rate-limiter）走类内 static 字典。
        services.TryAddScoped<IWorkspaceChannelConnectorRegistry, WorkspaceChannelConnectorRegistry>();

        // 治理 M-G02-C3 / C4：Web SDK + Open API connector 内置实现。
        services.AddScoped<IWorkspaceChannelConnector, WebSdkChannelConnector>();
        services.AddScoped<IWorkspaceChannelConnector, OpenApiChannelConnector>();
        // 治理 M-G02-C7 (S3)：飞书 connector
        services.AddScoped<IWorkspaceChannelConnector, Atlas.Infrastructure.Services.AiPlatform.Channels.Feishu.FeishuChannelConnector>();
        // 治理 M-G02-C11 (S4)：微信公众号 connector
        services.AddScoped<IWorkspaceChannelConnector, Atlas.Infrastructure.Services.AiPlatform.Channels.Wechat.WechatMpChannelConnector>();
        return services;
    }
}
