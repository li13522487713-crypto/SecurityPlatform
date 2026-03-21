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
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.DependencyInjection;

public static class AiPlatformServiceRegistration
{
    public static IServiceCollection AddAiPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiPlatformOptions>(configuration.GetSection("AiPlatform"));
        services.Configure<CodeExecutionOptions>(configuration.GetSection("CodeExecution"));
        services.AddHttpClient("AiPlatform", client => client.Timeout = TimeSpan.FromSeconds(120));

        services.AddSingleton<ILlmProvider>(sp =>
            CreateProvider(sp, "openai", "https://api.openai.com"));
        services.AddSingleton<ILlmProvider>(sp =>
            CreateProvider(sp, "deepseek", "https://api.deepseek.com"));
        services.AddSingleton<ILlmProvider>(sp =>
            CreateProvider(sp, "ollama", "http://localhost:11434"));

        services.AddSingleton<IEmbeddingProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiPlatformOptions>>().Value;
            if (options.Providers.TryGetValue("openai", out var option) && option.SupportsEmbedding)
            {
                return CreateProvider(sp, "openai", "https://api.openai.com");
            }

            throw new InvalidOperationException("No embedding provider is configured. Please configure AiPlatform:Providers.");
        });

        services.AddScoped<ModelConfigRepository>();
        services.AddScoped<AgentRepository>();
        services.AddScoped<AgentKnowledgeLinkRepository>();
        services.AddScoped<ConversationRepository>();
        services.AddScoped<ChatMessageRepository>();
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
        services.AddScoped<AiAppResourceCopyTaskRepository>();
        services.AddScoped<AiPromptTemplateRepository>();
        services.AddScoped<PersonalAccessTokenRepository>();
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
        services.AddScoped<IAgentChatService, AgentChatService>();
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddScoped<IAiDatabaseService, AiDatabaseService>();
        services.AddScoped<IAiVariableService, AiVariableService>();
        services.AddScoped<IAiPluginService, AiPluginService>();
        services.AddScoped<IAiAppService, AiAppService>();
        services.AddScoped<IAiPromptService, AiPromptService>();
        services.AddScoped<IPersonalAccessTokenService, PersonalAccessTokenService>();
        services.AddScoped<IAiMarketplaceService, AiMarketplaceService>();
        services.AddScoped<IAiSearchService, AiSearchService>();
        services.AddScoped<IAdminAiConfigService, AdminAiConfigService>();
        services.AddScoped<IAiWorkspaceService, AiWorkspaceService>();
        services.AddScoped<IAiShortcutCommandService, AiShortcutCommandService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IChunkService, ChunkService>();
        services.AddScoped<IRagRetrievalService, RagRetrievalService>();
        services.AddScoped<DocumentProcessingService>();
        services.AddScoped<AiWorkflowDefinitionRepository>();
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

        services.AddSingleton<IVectorStore, SqliteVectorStore>();

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

        services.AddSingleton<IChunkingService, FixedSizeChunkingService>();
        services.AddSingleton<BuiltInPluginMetadataProvider>();

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

    private static OpenAiCompatibleProvider CreateProvider(
        IServiceProvider serviceProvider,
        string providerName,
        string defaultBaseUrl)
    {
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OpenAiCompatibleProvider>>();
        var options = serviceProvider.GetRequiredService<IOptions<AiPlatformOptions>>().Value;
        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        options.Providers.TryGetValue(providerName, out var provider);
        provider ??= new AiProviderOption();
        var merged = new AiProviderOption
        {
            ApiKey = provider.ApiKey,
            BaseUrl = string.IsNullOrWhiteSpace(provider.BaseUrl) ? defaultBaseUrl : provider.BaseUrl,
            DefaultModel = provider.DefaultModel,
            SupportsEmbedding = provider.SupportsEmbedding
        };

        var client = factory.CreateClient("AiPlatform");
        return new OpenAiCompatibleProvider(providerName, merged, client, logger);
    }
}
