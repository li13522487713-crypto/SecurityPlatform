using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Mappings;
using Atlas.Application.LowCode.Repositories;
using Atlas.Application.LowCode.Validators;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories.LowCode;
using Atlas.Infrastructure.Services.LowCode;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Infrastructure.DependencyInjection;

/// <summary>
/// 低代码（M01）DI 注册集合：仓储 + 服务 + AutoMapper Profile + FluentValidation。
/// 在 ServiceCollectionExtensions.cs 由共享入口调用。
/// </summary>
public static class LowCodeServiceRegistration
{
    public static IServiceCollection AddLowCodeInfrastructure(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // M19 配额选项：仅当宿主传入 IConfiguration 时绑定，否则使用默认值
        if (configuration is not null)
        {
            services.Configure<LowCodeWorkflowQuotaOptions>(configuration.GetSection(LowCodeWorkflowQuotaOptions.SectionName));
        }
        else
        {
            services.AddOptions<LowCodeWorkflowQuotaOptions>();
        }
        // Repositories
        services.AddScoped<IAppDefinitionRepository, AppDefinitionRepository>();
        services.AddScoped<IPageDefinitionRepository, PageDefinitionRepository>();
        services.AddScoped<IAppVariableRepository, AppVariableRepository>();
        services.AddScoped<IAppContentParamRepository, AppContentParamRepository>();
        services.AddScoped<IAppVersionArchiveRepository, AppVersionArchiveRepository>();
        services.AddScoped<IAppPublishArtifactRepository, AppPublishArtifactRepository>();
        services.AddScoped<IAppResourceReferenceRepository, AppResourceReferenceRepository>();

        // Services
        services.AddScoped<IAppDefinitionQueryService, AppDefinitionQueryService>();
        services.AddScoped<IAppDefinitionCommandService, AppDefinitionCommandService>();
        services.AddScoped<IAppDraftLockService, AppDraftLockService>();
        services.AddScoped<IProjectIdeBootstrapService, ProjectIdeBootstrapService>();
        services.AddScoped<IProjectIdeDependencyGraphService, ProjectIdeDependencyGraphService>();
        services.AddScoped<IProjectIdePublishOrchestrator, ProjectIdePublishOrchestrator>();
        services.AddScoped<IAppComponentOverrideRepository, AppComponentOverrideRepository>();
        services.AddScoped<ILowCodeComponentManifestService, LowCodeComponentManifestService>();

        services.AddScoped<IPageDefinitionQueryService, PageDefinitionQueryService>();
        services.AddScoped<IPageDefinitionCommandService, PageDefinitionCommandService>();
        services.AddScoped<IAppVariableQueryService, AppVariableQueryService>();
        services.AddScoped<IAppVariableCommandService, AppVariableCommandService>();

        // M09 运行时工作流执行器 + 异步任务仓储
        services.AddScoped<IRuntimeWorkflowAsyncJobRepository, RuntimeWorkflowAsyncJobRepository>();
        services.AddScoped<IRuntimeWorkflowExecutor, RuntimeWorkflowExecutor>();

        // M10 运行时文件服务 + 上传会话仓储 + GC 作业
        services.AddScoped<ILowCodeAssetUploadSessionRepository, LowCodeAssetUploadSessionRepository>();
        services.AddScoped<IRuntimeFileService, RuntimeFileService>();
        services.AddSingleton<LowCodeAssetGcJob>();

        // M11 chatflow / session / 消息日志
        services.AddScoped<ILowCodeSessionRepository, LowCodeSessionRepository>();
        services.AddScoped<ILowCodeMessageLogRepository, LowCodeMessageLogRepository>();
        services.AddScoped<IRuntimeSessionService, RuntimeSessionService>();
        services.AddScoped<IRuntimeChatflowService, RuntimeChatflowService>();
        services.AddScoped<IRuntimeMessageLogService, RuntimeMessageLogService>();

        // M12 触发器 / Webview 域名
        services.AddScoped<ILowCodeTriggerRepository, LowCodeTriggerRepository>();
        services.AddScoped<ILowCodeWebviewDomainRepository, LowCodeWebviewDomainRepository>();
        services.AddScoped<IRuntimeTriggerService, RuntimeTriggerService>();
        services.AddScoped<IRuntimeWebviewDomainService, RuntimeWebviewDomainService>();

        // P4-2 服务端表达式预求值器（默认骨架；生产用 services.Replace 注入真实 jsonata.NET 实现）
        services.TryAddSingleton<IExpressionAuditor, NoopExpressionAuditor>();
        services.AddScoped<IServerSideExpressionEvaluator, ServerSideExpressionEvaluator>();

        // M13 dispatch + trace + 脱敏
        services.AddSingleton<ISensitiveMaskingService, SensitiveMaskingService>();
        services.AddScoped<IRuntimeTraceRepository, RuntimeTraceRepository>();
        services.AddScoped<IRuntimeTraceService, RuntimeTraceService>();
        services.AddScoped<IDispatchExecutor, DispatchExecutor>();

        // M14 版本管理 / 资源引用治理 / FAQ
        services.AddScoped<IAppFaqRepository, AppFaqRepository>();
        services.AddScoped<IAppVersioningService, AppVersioningService>();
        services.AddScoped<IResourceReferenceGuardService, ResourceReferenceGuardService>();
        services.AddScoped<IAppFaqService, AppFaqService>();

        // M15 渲染器能力差异化
        services.AddSingleton<ILowCodeRendererCapabilityService, LowCodeRendererCapabilityService>();

        // M17 发布服务（P2-2：默认 NoopPipeline；生产部署可 services.Replace 为 MinIO+CDN 实现）
        services.TryAddSingleton<IPublishBuildPipeline, NoopPublishBuildPipeline>();
        services.AddScoped<IAppPublishService, AppPublishService>();

        // M18 提示词模板 + 插件全域
        services.AddScoped<IPromptTemplateRepository, PromptTemplateRepository>();
        services.AddScoped<ILowCodePluginRepository, LowCodePluginRepository>();
        services.AddScoped<IPromptTemplateService, PromptTemplateService>();
        services.AddScoped<ILowCodePluginService, LowCodePluginService>();
        // M18 收尾：插件凭据 AES 加密保护（替换 base64 占位）
        services.AddSingleton<LowCodeCredentialProtector>();

        // Webview 域名验证 HttpClient（M12 → M17）
        services.AddHttpClient("lowcode-webview-verify");

        // M08 S08-3 Preview HMR 推送：默认 NoOp（无 hub 时安全跳过）；
        // PlatformHost / AppHost 在启动时通过 services.Replace 注入真实 SignalR 实现。
        services.TryAddSingleton<ILowCodePreviewSignal, NoOpLowCodePreviewSignal>();

        // Hangfire 桥接：cron 触发器实际执行类（由 RuntimeTriggerService AddOrUpdate 注册）
        services.AddTransient<LowCodeTriggerCronJob>();

        // M19 工作流父级工程能力
        services.AddScoped<IWorkflowGenerationService, WorkflowGenerationService>();
        services.AddScoped<IWorkflowBatchService, WorkflowBatchService>();
        services.AddScoped<IWorkflowCompositionService, WorkflowCompositionService>();
        services.AddScoped<IWorkflowQuotaService, WorkflowQuotaService>();

        // M07 S07-3：应用资源聚合
        services.AddScoped<IAppResourceCatalogService, AppResourceCatalogService>();
        services.AddScoped<ILowCodeAppResourceBindingService, LowCodeAppResourceBindingService>();

        // M07 S07-4：应用模板（CRUD + 共享市场）
        services.AddScoped<IAppTemplateRepository, AppTemplateRepository>();
        services.AddScoped<IAppTemplateService, AppTemplateService>();

        // M14 S14-4：资源引用增量索引器（应用 schema 变更时自动 reindex）
        services.AddScoped<IResourceReferenceIndex, ResourceReferenceIndex>();

        // M20 节点状态 + 双哲学（P3-7：DualOrchestrationEngine 改为 Scoped 以接 IChatClientFactory）
        services.AddScoped<INodeStateStore, NodeStateStore>();
        services.AddScoped<IDualOrchestrationEngine, DualOrchestrationEngine>();

        // P3-1：4 个智能体渠道适配器 + 渠道运行实体注册中心
        services.AddSingleton<IAgentRuntimeRegistry, Atlas.Infrastructure.Services.LowCode.AgentChannels.InMemoryAgentRuntimeRegistry>();
        services.AddScoped<IAgentChannelAdapter, Atlas.Infrastructure.Services.LowCode.AgentChannels.FeishuChannelAdapter>();
        services.AddScoped<IAgentChannelAdapter, Atlas.Infrastructure.Services.LowCode.AgentChannels.WeChatChannelAdapter>();
        services.AddScoped<IAgentChannelAdapter, Atlas.Infrastructure.Services.LowCode.AgentChannels.DouyinChannelAdapter>();
        services.AddScoped<IAgentChannelAdapter, Atlas.Infrastructure.Services.LowCode.AgentChannels.DoubaoChannelAdapter>();

        // M16 收尾：协同离线快照 Hangfire 周期任务
        services.AddSingleton<LowCodeCollabSnapshotJob>();

        // M19 收尾：批量 / 异步工作流后台作业
        services.AddSingleton<RuntimeWorkflowBackgroundJob>();

        // AutoMapper（Profile 在 Atlas.Application.LowCode 程序集中，按 marker 类型集中注册）
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<LowCodeMappingProfile>();
        });

        // FluentValidation（按程序集扫描 Application.LowCode 即可覆盖所有 *RequestValidator）
        services.AddValidatorsFromAssemblyContaining<AppDefinitionCreateRequestValidator>(
            includeInternalTypes: false);

        return services;
    }
}
