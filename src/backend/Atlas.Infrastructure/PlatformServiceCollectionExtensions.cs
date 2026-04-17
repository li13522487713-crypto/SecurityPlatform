using Atlas.Application.Plugins.Abstractions;
using Atlas.Core.Governance;
using Atlas.Infrastructure.DataScopes.AppRuntime;
using Atlas.Infrastructure.DataScopes.Platform;
using Atlas.Infrastructure.DependencyInjection;
using Atlas.Infrastructure.Services.PlatformRuntime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure;

public static class PlatformServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasInfrastructurePlatform(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLicenseInfrastructure(configuration);
        services.AddPlatformInfrastructure();
        services.AddGovernanceInfrastructure();
        services.AddAiPlatformDesignInfrastructure(configuration);

        services.AddScoped<IQuotaService, Atlas.Infrastructure.Governance.QuotaService>();
        services.AddScoped<ICanaryReleaseService, Atlas.Infrastructure.Governance.CanaryReleaseService>();
        services.AddScoped<IVersionFreezeService, Atlas.Infrastructure.Governance.VersionFreezeService>();

        services.AddScoped<Atlas.Infrastructure.Repositories.TenantDataSourceRepository>();
        services.AddScoped<Atlas.Application.System.Abstractions.ITenantDataSourceService,
            Atlas.Infrastructure.Services.TenantDataSourceService>();
        services.AddScoped<Atlas.Application.System.Abstractions.IAppMigrationService, Atlas.Infrastructure.Services.AppMigrationService>();
        services.AddScoped<Atlas.Application.System.Abstractions.ITenantDbConnectionFactory,
            Atlas.Infrastructure.Services.TenantDbConnectionFactory>();
        services.AddScoped<Atlas.Application.System.Abstractions.IAppDataSourceProvisioner,
            Atlas.Infrastructure.Services.AppDataSourceProvisioner>();
        services.AddScoped<Atlas.Infrastructure.Services.AppDatabaseProvisioningService>();
        services.AddScoped<Atlas.Application.System.Abstractions.IAppDbConnectionResolver>(sp =>
            (Atlas.Application.System.Abstractions.IAppDbConnectionResolver)sp.GetRequiredService<Atlas.Application.System.Abstractions.ITenantDbConnectionFactory>());
        services.AddScoped<Atlas.Infrastructure.Services.IAppDbScopeFactory, Atlas.Infrastructure.Services.AppDbScopeFactory>();
        services.AddScoped<IPlatformSqlSugarScopeFactory, PlatformSqlSugarScopeFactory>();
        services.AddScoped<IAppSqlSugarScopeFactory, AppSqlSugarScopeFactory>();
        services.AddSingleton<IPluginCatalogService, Atlas.Infrastructure.Services.PluginCatalogService>();
        services.AddScoped<Atlas.Application.System.Abstractions.ISqlQueryService, Atlas.Infrastructure.Services.SqlQueryService>();
        services.AddScoped<Atlas.Application.System.Abstractions.IMetadataLinkQueryService, Atlas.Infrastructure.Services.MetadataLinkQueryService>();

        services.AddScoped<Atlas.Application.Plugins.Repositories.IPluginConfigRepository, Atlas.Infrastructure.Repositories.PluginConfigRepository>();
        services.AddScoped<Atlas.Application.Plugins.Abstractions.IPluginConfigService, Atlas.Infrastructure.Services.PluginConfigService>();
        services.AddScoped<Atlas.Infrastructure.Plugins.PluginPackageService>();
        services.AddScoped<Atlas.Application.Plugins.Abstractions.IPluginMarketQueryService, Atlas.Infrastructure.Services.PluginMarketQueryService>();
        services.AddScoped<Atlas.Application.Plugins.Abstractions.IPluginMarketCommandService, Atlas.Infrastructure.Services.PluginMarketCommandService>();

        services.AddScoped<Atlas.Application.Templates.IComponentTemplateQueryService, Atlas.Infrastructure.Services.ComponentTemplateQueryService>();
        services.AddScoped<Atlas.Application.Templates.IComponentTemplateCommandService, Atlas.Infrastructure.Services.ComponentTemplateCommandService>();
        services.AddScoped<Atlas.Infrastructure.Services.TemplateSeedDataService>();

        services.AddScoped<Atlas.Application.Subscription.IPlanQueryService, Atlas.Infrastructure.Services.Subscription.PlanService>();
        services.AddScoped<Atlas.Application.Subscription.IPlanCommandService, Atlas.Infrastructure.Services.Subscription.PlanService>();
        services.AddScoped<Atlas.Application.Subscription.ISubscriptionService, Atlas.Infrastructure.Services.Subscription.SubscriptionService>();
        services.AddScoped<Atlas.Application.Observability.IAlertRuleService, Atlas.Infrastructure.Observability.AlertRuleService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.IAppPackageBuilder, FileSystemAppPackageBuilder>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.IAppPackageInstaller, FileSystemAppPackageInstaller>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.IAppReleaseOrchestrator, DefaultAppReleaseOrchestrator>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.IAppEntryQueryService, AppEntryQueryService>();
        services.AddHttpClient("app-runtime-health", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(3);
        });
        services.AddSingleton<Atlas.Application.Platform.Abstractions.IAppInstanceRegistry, FileSystemAppInstanceRegistry>();
        services.AddSingleton<Atlas.Application.Platform.Abstractions.IAppProcessManager, LocalChildProcessManager>();
        services.AddSingleton<Atlas.Application.Platform.Abstractions.IAppHealthProbe, HttpAppHealthProbe>();
        services.AddSingleton<Atlas.Application.Platform.Abstractions.IAppIngressResolver, DefaultAppIngressResolver>();
        services.AddSingleton<Atlas.Application.Platform.Abstractions.IAppLoginEntryResolver, DefaultAppLoginEntryResolver>();
        services.AddSingleton<Atlas.Application.Platform.Abstractions.IAppRuntimeSupervisor, AppRuntimeSupervisor>();
        services.AddHostedService<AppRuntimeSupervisorHostedService>();

        // Coze PRD Phase III - M1: 平台/个人级 in-memory 内容服务（首页/社区/通用管理/模板插件/个人设置）
        // 协议见 docs/mock-api-protocols.md，等运营后台落地后升级为持久化实现。
        services.AddSingleton<Atlas.Application.Coze.Abstractions.IHomeContentService,
            Atlas.Infrastructure.Services.Coze.InMemoryHomeContentService>();
        services.AddSingleton<Atlas.Application.Coze.Abstractions.ICommunityService,
            Atlas.Infrastructure.Services.Coze.InMemoryCommunityService>();
        services.AddSingleton<Atlas.Application.Coze.Abstractions.IPlatformGeneralService,
            Atlas.Infrastructure.Services.Coze.InMemoryPlatformGeneralService>();
        services.AddSingleton<Atlas.Application.Coze.Abstractions.IMarketSummaryService,
            Atlas.Infrastructure.Services.Coze.InMemoryMarketSummaryService>();
        services.AddSingleton<Atlas.Application.Coze.Abstractions.IMeSettingsService,
            Atlas.Infrastructure.Services.Coze.InMemoryMeSettingsService>();

        // Coze PRD Phase III - M2: 工作空间维度持久化对象（文件夹 / 发布渠道）
        services.AddScoped<Atlas.Infrastructure.Repositories.WorkspaceFolderRepository>();
        services.AddScoped<Atlas.Application.Coze.Abstractions.IWorkspaceFolderService,
            Atlas.Infrastructure.Services.Coze.WorkspaceFolderService>();
        services.AddScoped<Atlas.Infrastructure.Repositories.WorkspacePublishChannelRepository>();
        services.AddScoped<Atlas.Application.Coze.Abstractions.IWorkspacePublishChannelService,
            Atlas.Infrastructure.Services.Coze.WorkspacePublishChannelService>();

        // Coze PRD Phase III - M3: 任务中心 / 评测 / 测试集（in-memory，第二批接入 BatchProcess + EvaluationDataset）
        services.AddSingleton<Atlas.Application.Coze.Abstractions.IWorkspaceTaskService,
            Atlas.Infrastructure.Services.Coze.InMemoryWorkspaceTaskService>();
        services.AddSingleton<Atlas.Application.Coze.Abstractions.IWorkspaceEvaluationService,
            Atlas.Infrastructure.Services.Coze.InMemoryWorkspaceEvaluationService>();
        services.AddSingleton<Atlas.Application.Coze.Abstractions.IWorkspaceTestsetService,
            Atlas.Infrastructure.Services.Coze.InMemoryWorkspaceTestsetService>();

        return services;
    }
}
