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

        // Coze PRD Phase III - M1: 平台/个人级 in-memory 内容服务（社区/通用管理/模板插件/个人设置）
        // M4.5：HomeContent 已迁移为 PlatformHomeContentService（PlatformContent 表 + fallback）。
        services.AddScoped<Atlas.Infrastructure.Repositories.PlatformContentRepository>();
        services.AddScoped<Atlas.Application.Coze.Abstractions.IHomeContentService,
            Atlas.Infrastructure.Services.Coze.PlatformHomeContentService>();
        // M5.3：平台运营内容 CRUD。
        services.AddScoped<Atlas.Application.Coze.Abstractions.IPlatformContentAdminService,
            Atlas.Infrastructure.Services.Coze.PlatformContentAdminService>();
        // M5.5：Community / PlatformGeneral / MarketSummary 升级为持久化（读 PlatformContent + fallback）。
        // 依赖 Scoped 的 PlatformContentRepository，因此改注册为 Scoped。
        services.AddScoped<Atlas.Application.Coze.Abstractions.ICommunityService,
            Atlas.Infrastructure.Services.Coze.InMemoryCommunityService>();
        services.AddScoped<Atlas.Application.Coze.Abstractions.IPlatformGeneralService,
            Atlas.Infrastructure.Services.Coze.InMemoryPlatformGeneralService>();
        services.AddScoped<Atlas.Application.Coze.Abstractions.IMarketSummaryService,
            Atlas.Infrastructure.Services.Coze.InMemoryMarketSummaryService>();
        // M6.3：MeSettings 升级为持久化（UserSetting 表 + 跨进程偏好保留）。
        services.AddScoped<Atlas.Infrastructure.Repositories.UserSettingRepository>();
        services.AddScoped<Atlas.Application.Coze.Abstractions.IMeSettingsService,
            Atlas.Infrastructure.Services.Coze.PersistentMeSettingsService>();

        // Coze PRD Phase III - M2: 工作空间维度持久化对象（文件夹 / 发布渠道）
        services.AddScoped<Atlas.Infrastructure.Repositories.WorkspaceFolderRepository>();
        // Coze PRD Phase III - M4.2: 文件夹与对象的关联表 Repository
        services.AddScoped<Atlas.Infrastructure.Repositories.WorkspaceFolderItemRepository>();
        services.AddScoped<Atlas.Application.Coze.Abstractions.IWorkspaceFolderService,
            Atlas.Infrastructure.Services.Coze.WorkspaceFolderService>();
        services.AddScoped<Atlas.Infrastructure.Repositories.WorkspacePublishChannelRepository>();
        services.AddScoped<Atlas.Application.Coze.Abstractions.IWorkspacePublishChannelService,
            Atlas.Infrastructure.Services.Coze.WorkspacePublishChannelService>();

        // Coze PRD Phase III - M4.4: 任务中心持久化（复用 EvaluationTask）。
        services.AddScoped<Atlas.Application.Coze.Abstractions.IWorkspaceTaskService,
            Atlas.Infrastructure.Services.Coze.WorkspaceTaskService>();
        // Coze PRD Phase III - M5.1: 评测列表持久化（EvaluationTask + EvaluationResult 按 workspaceId 过滤）。
        services.AddScoped<Atlas.Application.Coze.Abstractions.IWorkspaceEvaluationService,
            Atlas.Infrastructure.Services.Coze.WorkspaceEvaluationService>();
        // Coze PRD Phase III - M4.3: 测试集持久化（复用 EvaluationDataset / EvaluationCase）。
        services.AddScoped<Atlas.Application.Coze.Abstractions.IWorkspaceTestsetService,
            Atlas.Infrastructure.Services.Coze.WorkspaceTestsetService>();

        // 系统初始化与迁移控制台（M5）
        // M8/A3：SetupRecoveryKeyService 同时按接口和具体类型注册，便于 SetupConsoleBootstrapInitializer 注入具体类型
        services.AddScoped<Atlas.Infrastructure.Services.SetupConsole.SetupRecoveryKeyService>();
        services.AddScoped<Atlas.Application.SetupConsole.Abstractions.ISetupRecoveryKeyService>(sp =>
            sp.GetRequiredService<Atlas.Infrastructure.Services.SetupConsole.SetupRecoveryKeyService>());
        services.AddScoped<Atlas.Application.SetupConsole.Abstractions.ISetupConsoleService,
            Atlas.Infrastructure.Services.SetupConsole.SetupConsoleService>();
        // ORM 跨库迁移引擎（M6）
        services.AddScoped<Atlas.Application.SetupConsole.Abstractions.IDataMigrationOrmService,
            Atlas.Infrastructure.Services.SetupConsole.OrmDataMigrationService>();
        // 控制台写操作审计（M7）
        services.AddScoped<Atlas.Infrastructure.Services.SetupConsole.SetupConsoleAuditWriter>();
        // M8/A2：迁移连接串加密保护
        services.AddSingleton<Atlas.Infrastructure.Services.SetupConsole.MigrationSecretProtector>();
        // M8/A3：启动时哈希 BootstrapAdmin 密码并落库
        services.AddHostedService<Atlas.Infrastructure.Services.SetupConsole.SetupConsoleBootstrapInitializer>();
        // M9/C5：切主时持久化数据库配置到 appsettings.runtime.json
        services.AddSingleton<Atlas.Infrastructure.Services.SetupConsole.RuntimeConfigPersistor>();

        return services;
    }
}
