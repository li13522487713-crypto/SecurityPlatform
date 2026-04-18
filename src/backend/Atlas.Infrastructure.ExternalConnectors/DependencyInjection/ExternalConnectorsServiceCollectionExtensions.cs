using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.DependencyInjection;
using Atlas.Connectors.Feishu;
using Atlas.Connectors.WeCom;
using Atlas.Infrastructure.ExternalConnectors.HostedServices;
using Atlas.Infrastructure.ExternalConnectors.Repositories;
using Atlas.Infrastructure.ExternalConnectors.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Atlas.Infrastructure.ExternalConnectors.DependencyInjection;

public static class ExternalConnectorsServiceCollectionExtensions
{
    /// <summary>
    /// 注册 ExternalConnectors 基础能力（Application/Infrastructure 端）。
    /// 调用方仍需：
    /// 1. 注册 ISecretProtector 桥接实现（PlatformHost 中桥接到 DataProtectionService）；
    /// 2. 注册 ILocalUserDirectory 桥接实现（PlatformHost 中桥接到 IUserAccountRepository）；
    /// 3. 注册 IConnectorJwtIssuer 桥接实现（PlatformHost 中桥接到 JwtAuthTokenService）；
    /// 4. 通过 AddWeComConnector / AddFeishuConnector 加挂 provider 实现 + HttpClientFactory 命名客户端。
    /// </summary>
    public static IServiceCollection AddExternalConnectorsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddConnectorsCore();

        services.TryAddScoped<IExternalIdentityProviderRepository, ExternalIdentityProviderRepository>();
        services.TryAddScoped<IExternalIdentityBindingRepository, ExternalIdentityBindingRepository>();
        services.TryAddScoped<IExternalIdentityBindingAuditRepository, ExternalIdentityBindingAuditRepository>();
        services.TryAddScoped<IExternalDirectoryMirrorRepository, ExternalDirectoryMirrorRepository>();
        services.TryAddScoped<IExternalDirectorySyncJobRepository, ExternalDirectorySyncJobRepository>();
        services.TryAddScoped<IExternalDirectorySyncDiffRepository, ExternalDirectorySyncDiffRepository>();
        services.TryAddScoped<IExternalApprovalTemplateCacheRepository, ExternalApprovalTemplateCacheRepository>();
        services.TryAddScoped<IExternalApprovalTemplateMappingRepository, ExternalApprovalTemplateMappingRepository>();
        services.TryAddScoped<IExternalApprovalInstanceLinkRepository, ExternalApprovalInstanceLinkRepository>();

        services.TryAddScoped<IExternalIdentityProviderQueryService, ExternalIdentityProviderQueryService>();
        services.TryAddScoped<IExternalIdentityProviderCommandService, ExternalIdentityProviderCommandService>();
        services.TryAddScoped<IExternalIdentityBindingService, ExternalIdentityBindingService>();
        services.TryAddScoped<IExternalDirectorySyncService, ExternalDirectorySyncService>();
        services.TryAddScoped<IExternalApprovalTemplateService, ExternalApprovalTemplateService>();
        services.TryAddScoped<IExternalApprovalDispatchService, ExternalApprovalDispatchService>();
        services.TryAddScoped<IExternalCallbackEventRepository, ExternalCallbackEventRepository>();
        services.TryAddScoped<IConnectorCallbackInboxService, ConnectorCallbackInboxService>();
        services.TryAddScoped<IConnectorOAuthFlowService, ConnectorOAuthFlowService>();

        services.AddSingleton<ExternalDirectoryRecurringSyncRunner>();
        services.AddHostedService<ExternalDirectoryFullSyncHostedService>();

        // 注册审批通知 Sender：让现有 IApprovalNotificationSender 多渠道总线自动新增 WeCom / Feishu 两条路径。
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationSender, WeComApprovalNotificationSender>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationSender, FeishuApprovalNotificationSender>();

        return services;
    }

    /// <summary>
    /// 注册企微 provider：4 大能力实现 + 命名 HttpClient + RuntimeOptionsResolver。
    /// </summary>
    public static IServiceCollection AddWeComConnector(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient(WeComApiClient.HttpClientName);
        services.TryAddSingleton<WeComApiClient>();
        services.TryAddScoped<IConnectorRuntimeOptionsResolver<WeComRuntimeOptions>, WeComRuntimeOptionsResolver>();
        services.AddSingleton<IExternalIdentityProvider, WeComIdentityProvider>();
        services.AddSingleton<IExternalDirectoryProvider, WeComDirectoryProvider>();
        services.AddSingleton<IExternalApprovalProvider, WeComApprovalProvider>();
        services.AddSingleton<IExternalMessagingProvider, WeComMessagingProvider>();
        services.AddSingleton<IConnectorEventVerifier, WeComCallbackVerifier>();
        return services;
    }

    /// <summary>
    /// 注册飞书 provider：4 大能力实现 + 命名 HttpClient + RuntimeOptionsResolver。
    /// </summary>
    public static IServiceCollection AddFeishuConnector(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient(FeishuApiClient.HttpClientName);
        services.TryAddSingleton<FeishuApiClient>();
        services.TryAddScoped<IConnectorRuntimeOptionsResolver<FeishuRuntimeOptions>, FeishuRuntimeOptionsResolver>();
        services.AddSingleton<IExternalIdentityProvider, FeishuIdentityProvider>();
        services.AddSingleton<IExternalDirectoryProvider, FeishuDirectoryProvider>();
        services.AddSingleton<IExternalApprovalProvider, FeishuApprovalProvider>();
        services.AddSingleton<IExternalMessagingProvider, FeishuMessagingProvider>();
        services.AddSingleton<IConnectorEventVerifier, FeishuEventVerifier>();
        return services;
    }
}
