using Atlas.AppHost.ExternalConnectors.Bridges;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Mappings;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Application.ExternalConnectors.Validators;
using Atlas.Infrastructure.ExternalConnectors.DependencyInjection;
using Atlas.Sdk.ConnectorPlugins.DependencyInjection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.AppHost.ExternalConnectors;

public static class AppHostExternalConnectorsExtensions
{
    /// <summary>
    /// 在 AppHost.Program.cs 中调用一次：装配数据平面所需的 ExternalConnectors 能力。
    ///
    /// 与 PlatformHost 的关键差异：
    /// 1. AddExternalConnectorsCore(includeHostedServices: false)：目录全量同步与回调重试两类后台 Job 仅在 PlatformHost 运行，
    ///    避免双进程并发执行造成的数据竞态。
    /// 2. 注入 AppHostConnectorJwtIssuerNoop：AppHost 不签发 JWT、不持有会话仓储依赖；OAuth 回调入口仅在 PlatformHost。
    ///
    /// 包含的能力：连接器 provider（WeCom/Feishu/DingTalk）、工作流插件节点（ExternalSendMessage / ExternalCreateApproval 等）、
    /// 审批通知 Sender（接入 IApprovalNotificationSender 总线）、审批 fan-out 处理器。
    /// </summary>
    public static IServiceCollection AddAppHostExternalConnectors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExternalConnectorsCore(includeHostedServices: false);
        services.AddWeComConnector();
        services.AddFeishuConnector();
        services.AddDingTalkConnector();
        services.AddConnectorPluginNodes();

        services.TryAddScoped<ISecretProtector, ConnectorSecretProtectorBridge>();
        services.TryAddScoped<ILocalUserDirectory, ConnectorLocalUserDirectoryBridge>();
        services.TryAddScoped<IConnectorJwtIssuer, AppHostConnectorJwtIssuerNoop>();
        services.TryAddSingleton<Atlas.Infrastructure.ExternalConnectors.HostedServices.ITenantContextWriter, ConnectorTenantContextWriterBridge>();

        services.AddAutoMapper(typeof(ExternalConnectorsMappingProfile).Assembly);

        services.AddScoped<IValidator<ExternalIdentityProviderCreateRequest>, ExternalIdentityProviderCreateRequestValidator>();
        services.AddScoped<IValidator<ExternalIdentityProviderUpdateRequest>, ExternalIdentityProviderUpdateRequestValidator>();
        services.AddScoped<IValidator<ExternalIdentityProviderRotateSecretRequest>, ExternalIdentityProviderRotateSecretRequestValidator>();
        services.AddScoped<IValidator<ManualBindingRequest>, ManualBindingRequestValidator>();
        services.AddScoped<IValidator<BindingConflictResolutionRequest>, BindingConflictResolutionRequestValidator>();
        services.AddScoped<IValidator<OAuthInitiationRequest>, OAuthInitiationRequestValidator>();
        services.AddScoped<IValidator<OAuthCallbackRequest>, OAuthCallbackRequestValidator>();

        return services;
    }
}
