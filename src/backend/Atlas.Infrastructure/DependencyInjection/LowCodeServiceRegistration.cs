using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Mappings;
using Atlas.Application.LowCode.Repositories;
using Atlas.Application.LowCode.Validators;
using Atlas.Infrastructure.Repositories.LowCode;
using Atlas.Infrastructure.Services.LowCode;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

/// <summary>
/// 低代码（M01）DI 注册集合：仓储 + 服务 + AutoMapper Profile + FluentValidation。
/// 在 ServiceCollectionExtensions.cs 由共享入口调用。
/// </summary>
public static class LowCodeServiceRegistration
{
    public static IServiceCollection AddLowCodeInfrastructure(this IServiceCollection services)
    {
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

        // M17 发布服务
        services.AddScoped<IAppPublishService, AppPublishService>();

        // M18 提示词模板 + 插件全域
        services.AddScoped<IPromptTemplateRepository, PromptTemplateRepository>();
        services.AddScoped<ILowCodePluginRepository, LowCodePluginRepository>();
        services.AddScoped<IPromptTemplateService, PromptTemplateService>();
        services.AddScoped<ILowCodePluginService, LowCodePluginService>();
        // M18 收尾：插件凭据 AES 加密保护（替换 base64 占位）
        services.AddSingleton<LowCodeCredentialProtector>();

        // M19 工作流父级工程能力
        services.AddScoped<IWorkflowGenerationService, WorkflowGenerationService>();
        services.AddScoped<IWorkflowBatchService, WorkflowBatchService>();
        services.AddScoped<IWorkflowCompositionService, WorkflowCompositionService>();
        services.AddSingleton<IWorkflowQuotaService, WorkflowQuotaService>();

        // M07 S07-3：应用资源聚合
        services.AddScoped<IAppResourceCatalogService, AppResourceCatalogService>();

        // M07 S07-4：应用模板（CRUD + 共享市场）
        services.AddScoped<IAppTemplateRepository, AppTemplateRepository>();
        services.AddScoped<IAppTemplateService, AppTemplateService>();

        // M20 节点状态 + 双哲学
        services.AddScoped<INodeStateStore, NodeStateStore>();
        services.AddSingleton<IDualOrchestrationEngine, DualOrchestrationEngine>();

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
