using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Application.AgentTeam.Abstractions;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;
using Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;
using Atlas.Infrastructure.Services.WorkflowEngine;
using Atlas.Infrastructure.Services.AgentTeam;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

public static class AiDesignServiceRegistration
{
    /// <summary>
    /// AI 平台设计态层：Agent 定义管理、模型配置、知识库管理、评测、
    /// AI 工作流设计、Prompt 模板、插件管理等。仅 PlatformHost 注册。
    /// </summary>
    public static IServiceCollection AddAiPlatformDesignInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentFrameworkOptions>(configuration.GetSection("AgentFramework"));

        services.AddScoped<ModelConfigRepository>();
        services.AddScoped<AgentRepository>();
        services.AddScoped<AgentKnowledgeLinkRepository>();
        services.AddScoped<AgentPluginBindingRepository>();
        services.AddScoped<TeamAgentRepository>();
        services.AddScoped<TeamAgentPublicationRepository>();
        services.AddScoped<TeamAgentTemplateRepository>();
        services.AddScoped<TeamAgentTemplateMemberRepository>();
        services.AddScoped<TeamAgentSchemaDraftRepository>();
        services.AddScoped<TeamAgentSchemaDraftExecutionAuditRepository>();
        services.AddScoped<KnowledgeBaseRepository>();
        services.AddScoped<KnowledgeDocumentRepository>();
        services.AddScoped<DocumentChunkRepository>();
        services.AddScoped<AiDatabaseRepository>();
        services.AddScoped<AiDatabaseRecordRepository>();
        services.AddScoped<AiDatabaseImportTaskRepository>();
        services.AddScoped<AiVariableRepository>();
        services.AddScoped<AiPluginRepository>();
        services.AddScoped<AiPluginApiRepository>();
        services.AddScoped<AiAppRepository>();
        services.AddScoped<AiAppPublishRecordRepository>();
        services.AddScoped<AgentPublicationRepository>();
        services.AddScoped<EvaluationDatasetRepository>();
        services.AddScoped<EvaluationCaseRepository>();
        services.AddScoped<EvaluationTaskRepository>();
        services.AddScoped<EvaluationResultRepository>();
        services.AddScoped<ApiCallLogRepository>();
        services.AddScoped<AiAppResourceCopyTaskRepository>();
        services.AddScoped<AiPromptTemplateRepository>();
        services.AddScoped<PersonalAccessTokenRepository>();
        services.AddScoped<OpenApiProjectRepository>();
        services.AddScoped<AiProductCategoryRepository>();
        services.AddScoped<AiMarketplaceProductRepository>();
        services.AddScoped<AiMarketplaceFavoriteRepository>();
        services.AddScoped<AiRecentEditRepository>();
        services.AddScoped<AiWorkspaceRepository>();
        services.AddScoped<AiShortcutCommandRepository>();
        services.AddScoped<AiBotPopupInfoRepository>();
        services.AddScoped<AgentTeamRepository>();
        services.AddScoped<SubAgentRepository>();
        services.AddScoped<OrchestrationNodeRepository>();
        services.AddScoped<TeamVersionRepository>();
        services.AddScoped<IAgentTeamRepository>(sp => sp.GetRequiredService<AgentTeamRepository>());
        services.AddScoped<ISubAgentRepository>(sp => sp.GetRequiredService<SubAgentRepository>());
        services.AddScoped<IOrchestrationNodeRepository>(sp => sp.GetRequiredService<OrchestrationNodeRepository>());
        services.AddScoped<ITeamVersionRepository>(sp => sp.GetRequiredService<TeamVersionRepository>());

        services.AddScoped<IModelConfigCommandService, ModelConfigCommandService>();
        services.AddScoped<IModelConfigQueryService, ModelConfigQueryService>();
        services.AddScoped<IAgentCommandService, AgentCommandService>();
        services.AddScoped<IAgentQueryService, AgentQueryService>();
        services.AddScoped<ITeamAgentService, TeamAgentService>();
        services.AddScoped<ITeamAgentPublicationService, TeamAgentPublicationService>();
        services.AddScoped<ITeamAgentSchemaDraftComposer, TeamAgentSchemaDraftComposer>();
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddScoped<IAiDatabaseService, AiDatabaseService>();
        services.AddScoped<IAiVariableService, AiVariableService>();
        services.AddScoped<IAiPluginService, AiPluginService>();
        services.AddScoped<IAiAppService, AiAppService>();
        services.AddScoped<IAiPromptService, AiPromptService>();
        services.AddScoped<IPersonalAccessTokenService, PersonalAccessTokenService>();
        services.AddScoped<IOpenApiProjectService, OpenApiProjectService>();
        services.AddScoped<IOpenApiCallLogService, OpenApiCallLogService>();
        services.AddScoped<IAiMarketplaceService, AiMarketplaceService>();
        services.AddScoped<IAgentPublicationService, AgentPublicationService>();
        services.AddScoped<IAgentTeamQueryService, AgentTeamService>();
        services.AddScoped<IAgentTeamCommandService, AgentTeamService>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddScoped<IEvaluationJobService, EvaluationJobService>();
        services.AddScoped<IAdminAiConfigService, AdminAiConfigService>();
        services.AddScoped<IAiWorkspaceService, AiWorkspaceService>();
        services.AddScoped<IAiShortcutCommandService, AiShortcutCommandService>();
        services.AddSingleton<BuiltInPluginMetadataProvider>();
        services.AddSingleton<OpenApiProjectRateLimiter>();

        services.AddScoped<IAgentRuntimeFactory, AgentRuntimeFactory>();
        services.AddScoped<AiPluginRuntimeExecutor>();
        services.AddScoped<AgentKernelAugmentationService>();

        services.AddScoped<AiWorkflowDefinitionRepository>();
        services.AddScoped<AiWorkflowSnapshotRepository>();
        services.AddScoped<IAiWorkflowDesignService, AiWorkflowDesignService>();
        services.AddSingleton<AiWorkflowDslBuilder>();

        services.AddTransient<LlmStep>();
        services.AddTransient<PluginStep>();
        services.AddTransient<CodeRunnerStep>();
        services.AddTransient<KnowledgeRetrieverStep>();
        services.AddTransient<TextProcessorStep>();
        services.AddTransient<HttpRequesterStep>();
        services.AddTransient<OutputEmitterStep>();

        services.AddScoped<IWorkflowV2CommandService, WorkflowV2CommandService>();
        services.AddScoped<IWorkflowV2QueryService, WorkflowV2QueryService>();

        return services;
    }
}
