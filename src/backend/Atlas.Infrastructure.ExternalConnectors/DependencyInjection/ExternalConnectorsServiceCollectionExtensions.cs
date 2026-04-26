using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.DependencyInjection;
using Atlas.Connectors.DingTalk;
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
    /// 1. 注册 ISecretProtector 桥接实现（AppHost 中桥接到 DataProtectionService）；
    /// 2. 注册 ILocalUserDirectory 桥接实现（AppHost 中桥接到 IUserAccountRepository）；
    /// 3. 注册 IConnectorJwtIssuer 桥接实现（AppHost 中桥接到 JwtAuthTokenService）；
    /// 4. 通过 AddWeComConnector / AddFeishuConnector 加挂 provider 实现 + HttpClientFactory 命名客户端。
    ///
    /// <paramref name="includeHostedServices"/> 控制是否在本进程内启用目录全量同步与回调重试两类后台 Job。
    /// AppHost 按部署职责决定是否开启，单宿主模式默认可开启；多实例部署需避免重复执行造成数据竞态。
    /// </summary>
    public static IServiceCollection AddExternalConnectorsCore(
        this IServiceCollection services,
        bool includeHostedServices = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddConnectorsCore();

        services.TryAddScoped<IConnectorRuntimeOptionsAccessor, ConnectorRuntimeOptionsAccessor>();

        services.TryAddScoped<IExternalIdentityProviderRepository, ExternalIdentityProviderRepository>();
        services.TryAddScoped<IExternalIdentityBindingRepository, ExternalIdentityBindingRepository>();
        services.TryAddScoped<IExternalIdentityBindingAuditRepository, ExternalIdentityBindingAuditRepository>();
        services.TryAddScoped<IExternalDirectoryMirrorRepository, ExternalDirectoryMirrorRepository>();
        services.TryAddScoped<IExternalDirectorySyncJobRepository, ExternalDirectorySyncJobRepository>();
        services.TryAddScoped<IExternalDirectorySyncDiffRepository, ExternalDirectorySyncDiffRepository>();
        services.TryAddScoped<IExternalApprovalTemplateCacheRepository, ExternalApprovalTemplateCacheRepository>();
        services.TryAddScoped<IExternalApprovalTemplateMappingRepository, ExternalApprovalTemplateMappingRepository>();
        services.TryAddScoped<IExternalApprovalInstanceLinkRepository, ExternalApprovalInstanceLinkRepository>();
        services.TryAddScoped<IExternalMessageDispatchRepository, ExternalMessageDispatchRepository>();

        services.TryAddScoped<IExternalIdentityProviderQueryService, ExternalIdentityProviderQueryService>();
        services.TryAddScoped<IExternalIdentityProviderCommandService, ExternalIdentityProviderCommandService>();
        services.TryAddScoped<IExternalIdentityBindingService, ExternalIdentityBindingService>();
        services.TryAddScoped<IExternalDirectorySyncService, ExternalDirectorySyncService>();
        services.TryAddScoped<IExternalApprovalTemplateService, ExternalApprovalTemplateService>();
        services.TryAddScoped<IExternalApprovalDispatchService, ExternalApprovalDispatchService>();
        services.TryAddScoped<IExternalMessagingService, ExternalMessagingService>();
        services.TryAddScoped<IExternalCallbackEventRepository, ExternalCallbackEventRepository>();
        services.TryAddScoped<IConnectorCallbackInboxService, ConnectorCallbackInboxService>();
        services.TryAddScoped<IConnectorOAuthFlowService, ConnectorOAuthFlowService>();

        if (includeHostedServices)
        {
            services.AddSingleton<ExternalDirectoryRecurringSyncRunner>();
            services.AddHostedService<ExternalDirectoryFullSyncHostedService>();
            services.AddHostedService<ExternalCallbackInboxRetryHostedService>();
        }

        // 注册审批通知 Sender：让现有 IApprovalNotificationSender 多渠道总线自动新增 WeCom / Feishu / DingTalk 三条路径。
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationSender, WeComApprovalNotificationSender>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationSender, FeishuApprovalNotificationSender>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationSender, DingTalkApprovalNotificationSender>();

        // 把外部协同 fan-out 接入现有 ApprovalEventPublisher 的 IApprovalEventHandler 多注册总线。
        // 本地审批 Started/Completed/Rejected/Canceled/Task* 事件触发后，dispatch service 会按 IntegrationMode 决定是否推外部。
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalEventHandler, ExternalApprovalFanoutHandler>();

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

    /// <summary>
    /// 注册钉钉 provider：4 大能力实现 + 命名 HttpClient + RuntimeOptionsResolver + AES 回调验证器。
    /// 底层基于 AlibabaCloud.SDK.Dingtalk + 我们自己的 v1 老版/v1.0 新版 OpenAPI 适配层。
    /// </summary>
    public static IServiceCollection AddDingTalkConnector(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient(DingTalkApiClient.HttpClientName);
        services.TryAddSingleton<DingTalkApiClient>();
        services.TryAddScoped<IConnectorRuntimeOptionsResolver<DingTalkRuntimeOptions>, DingTalkRuntimeOptionsResolver>();
        services.AddSingleton<IExternalIdentityProvider, DingTalkIdentityProvider>();
        services.AddSingleton<IExternalDirectoryProvider, DingTalkDirectoryProvider>();
        services.AddSingleton<IExternalApprovalProvider, DingTalkApprovalProvider>();
        services.AddSingleton<IExternalMessagingProvider, DingTalkMessagingProvider>();
        services.AddSingleton<IConnectorEventVerifier, DingTalkCallbackVerifier>();
        return services;
    }
}
