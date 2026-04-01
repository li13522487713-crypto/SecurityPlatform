using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;
using Atlas.Infrastructure.Services.AiPlatform.CodeExecution;
using Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;
using Atlas.Infrastructure.Services.AiPlatform.Parsers;
using Atlas.Infrastructure.Services.WorkflowEngine;
using Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

public static class AiPlatformServiceRegistration
{
    public static IServiceCollection AddAiPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiPlatformOptions>(configuration.GetSection("AiPlatform"));
        services.Configure<AgentFrameworkOptions>(configuration.GetSection("AgentFramework"));
        services.Configure<CodeExecutionOptions>(configuration.GetSection("CodeExecution"));
        services.AddHttpClient("AiPlatform", client => client.Timeout = TimeSpan.FromSeconds(120));

        services.AddScoped<ModelConfigRepository>();
        services.AddScoped<AgentRepository>();
        services.AddScoped<AgentKnowledgeLinkRepository>();
        services.AddScoped<AgentPluginBindingRepository>();
        services.AddScoped<ConversationRepository>();
        services.AddScoped<ChatMessageRepository>();
        services.AddScoped<TeamAgentRepository>();
        services.AddScoped<TeamAgentPublicationRepository>();
        services.AddScoped<TeamAgentTemplateRepository>();
        services.AddScoped<TeamAgentTemplateMemberRepository>();
        services.AddScoped<TeamAgentConversationRepository>();
        services.AddScoped<TeamAgentMessageRepository>();
        services.AddScoped<TeamAgentExecutionRepository>();
        services.AddScoped<TeamAgentExecutionStepRepository>();
        services.AddScoped<TeamAgentSchemaDraftRepository>();
        services.AddScoped<TeamAgentSchemaDraftExecutionAuditRepository>();
        services.AddScoped<LongTermMemoryRepository>();
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
        services.AddScoped<MultiAgentOrchestrationRepository>();
        services.AddScoped<MultiAgentExecutionRepository>();
        services.AddScoped<MultimodalAssetRepository>();
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
        services.AddScoped<IModelConfigCommandService, ModelConfigCommandService>();
        services.AddScoped<IModelConfigQueryService, ModelConfigQueryService>();
        services.AddScoped<IAgentCommandService, AgentCommandService>();
        services.AddScoped<IAgentQueryService, AgentQueryService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<ITeamAgentService, TeamAgentService>();
        services.AddScoped<ITeamAgentPublicationService, TeamAgentPublicationService>();
        services.AddScoped<IChatClientFactory, ChatClientFactory>();
        services.AddScoped<IKernelFactory, KernelFactory>();
        services.AddScoped<IAgentRuntimeFactory, AgentRuntimeFactory>();
        services.AddScoped<ITeamAgentOrchestrationRuntime, FrameworkAwareTeamAgentOrchestrationRuntime>();
        services.AddScoped<ITeamAgentSchemaDraftComposer, TeamAgentSchemaDraftComposer>();
        services.AddScoped<IAgentChatService, AgentChatService>();
        services.AddScoped<IAgentToolCallService, AgentToolCallService>();
        services.AddScoped<ILongTermMemoryExtractionService, LongTermMemoryExtractionService>();
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
        services.AddScoped<IAiSearchService, AiSearchService>();
        services.AddScoped<IAiMemoryService, AiMemoryService>();
        services.AddScoped<IAgentPublicationService, AgentPublicationService>();
        services.AddScoped<IMultiAgentOrchestrationService, MultiAgentOrchestrationService>();
        services.AddScoped<IMultimodalService, MultimodalService>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddScoped<IEvaluationJobService, EvaluationJobService>();
        services.AddScoped<IAdminAiConfigService, AdminAiConfigService>();
        services.AddScoped<IAiWorkspaceService, AiWorkspaceService>();
        services.AddScoped<IAiShortcutCommandService, AiShortcutCommandService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IChunkService, ChunkService>();
        services.AddScoped<BM25RetrievalService>();
        services.AddScoped<HybridRetrievalService>();
        services.AddScoped<IRagRetrievalService, RagRetrievalService>();
        services.AddScoped<DocumentProcessingService>();
        services.AddScoped<AiWorkflowDefinitionRepository>();
        services.AddScoped<AiWorkflowSnapshotRepository>();
        services.AddScoped<IAiWorkflowDesignService, AiWorkflowDesignService>();
        services.AddScoped<IAiWorkflowExecutionService, AiWorkflowExecutionService>();
        services.AddSingleton<AiWorkflowDslBuilder>();

        services.AddTransient<LlmStep>();
        services.AddTransient<PluginStep>();
        services.AddTransient<CodeRunnerStep>();
        services.AddTransient<KnowledgeRetrieverStep>();
        services.AddTransient<TextProcessorStep>();
        services.AddTransient<HttpRequesterStep>();
        services.AddTransient<OutputEmitterStep>();

        services.AddScoped<ILlmProviderFactory, LlmProviderFactory>();
        services.AddScoped<IEmbeddingProvider>(sp =>
            sp.GetRequiredService<ILlmProviderFactory>().GetEmbeddingProvider());

        services.AddSingleton<IVectorDbClient, SqliteVectorDbClient>();
        services.AddSingleton<IVectorDbClient, QdrantVectorDbClient>();
        services.AddSingleton<IVectorStore, VectorStore>();

        services.AddSingleton<TxtDocumentParser>();
        services.AddSingleton<PdfDocumentParser>();
        services.AddSingleton<DocxDocumentParser>();
        services.AddSingleton<MarkdownDocumentParser>();
        services.AddSingleton<SpreadsheetDocumentParser>();
        services.AddSingleton<JsonDocumentParser>();

        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<TxtDocumentParser>());
        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<PdfDocumentParser>());
        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<DocxDocumentParser>());
        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<MarkdownDocumentParser>());
        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<SpreadsheetDocumentParser>());
        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<JsonDocumentParser>());
        services.AddSingleton<DocumentParserComposite>();
        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<DocumentParserComposite>());

        services.AddSingleton<IChunkingStrategy, FixedSizeChunkingService>();
        services.AddSingleton<IChunkingStrategy, SemanticChunkingService>();
        services.AddSingleton<IChunkingStrategy, RecursiveChunkingService>();
        services.AddSingleton<IChunkingService, ChunkingService>();
        services.AddSingleton<BuiltInPluginMetadataProvider>();
        services.AddSingleton<IOpenApiPluginParser, OpenApiPluginParser>();
        services.AddSingleton<MultiAgentExecutionTracker>();
        services.AddSingleton<OpenApiProjectRateLimiter>();

        // ── Workflow V2: DAG Engine ──
        services.AddScoped<IWorkflowMetaRepository, WorkflowMetaRepository>();
        services.AddScoped<IWorkflowDraftRepository, WorkflowDraftRepository>();
        services.AddScoped<IWorkflowVersionRepository, WorkflowVersionRepository>();
        services.AddScoped<IWorkflowExecutionRepository, WorkflowExecutionRepository>();
        services.AddScoped<IWorkflowNodeExecutionRepository, WorkflowNodeExecutionRepository>();

        services.AddScoped<DagExecutor>();
        services.AddSingleton<WorkflowExecutionCancellationRegistry>();
        services.AddScoped<NodeExecutorRegistry>();
        services.AddScoped<INodeExecutor, EntryNodeExecutor>();
        services.AddScoped<INodeExecutor, ExitNodeExecutor>();
        services.AddScoped<INodeExecutor, SelectorNodeExecutor>();
        services.AddScoped<INodeExecutor, LlmNodeExecutor>();
        services.AddScoped<INodeExecutor, AgentNodeExecutor>();
        services.AddScoped<INodeExecutor, PluginNodeExecutor>();
        services.AddScoped<INodeExecutor, SubWorkflowNodeExecutor>();
        services.AddScoped<INodeExecutor, LoopNodeExecutor>();
        services.AddScoped<INodeExecutor, CodeRunnerNodeExecutor>();
        services.AddScoped<INodeExecutor, HttpRequesterNodeExecutor>();
        services.AddScoped<INodeExecutor, TextProcessorNodeExecutor>();
        services.AddScoped<INodeExecutor, DatabaseQueryNodeExecutor>();
        services.AddScoped<INodeExecutor, AssignVariableNodeExecutor>();
        services.AddScoped<INodeExecutor, VariableAggregatorNodeExecutor>();
        services.AddScoped<INodeExecutor, JsonSerializationNodeExecutor>();
        services.AddScoped<INodeExecutor, JsonDeserializationNodeExecutor>();

        services.AddScoped<IWorkflowV2CommandService, WorkflowV2CommandService>();
        services.AddScoped<IWorkflowV2QueryService, WorkflowV2QueryService>();
        services.AddScoped<IWorkflowV2ExecutionService, WorkflowV2ExecutionService>();
        services.AddHttpClient("WorkflowEngine", client => client.Timeout = TimeSpan.FromSeconds(30));

        services.AddTransient<DirectPythonExecutor>();
        services.AddTransient<DockerPythonExecutor>();
        services.AddTransient<SandboxedPythonExecutor>();
        services.AddScoped<ICodeExecutionService>(sp =>
            sp.GetRequiredService<SandboxedPythonExecutor>());

        return services;
    }

}
