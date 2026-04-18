using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Abstractions.Knowledge;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;
using Atlas.Infrastructure.Services.AiPlatform.CodeExecution;
using Atlas.Infrastructure.Services.AiPlatform.Parsers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

public static class AiCoreServiceRegistration
{
    /// <summary>
    /// AI 共享核心层：模型调用抽象、RAG 管道、向量检索、文档解析、代码执行。
    /// PlatformHost 和 AppHost 均需注册。
    /// </summary>
    public static IServiceCollection AddAiCoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiPlatformOptions>(configuration.GetSection("AiPlatform"));
        services.Configure<CodeExecutionOptions>(configuration.GetSection("CodeExecution"));
        services.AddHttpClient("AiPlatform", client => client.Timeout = TimeSpan.FromSeconds(120));

        services.AddScoped<IChatClientFactory, ChatClientFactory>();
        services.AddScoped<IKernelFactory, KernelFactory>();
        services.AddScoped<ILlmProviderFactory, LlmProviderFactory>();
        services.AddScoped<IEmbeddingProvider>(sp =>
            sp.GetRequiredService<ILlmProviderFactory>().GetEmbeddingProvider());

        services.AddSingleton<IVectorDbClient, SqliteVectorDbClient>();
        services.AddSingleton<IVectorDbClient, QdrantVectorDbClient>();
        services.AddSingleton<IVectorStore, VectorStore>();

        services.AddScoped<IQueryRewriter, QueryRewriterService>();
        services.AddScoped<VectorRetrieverService>();
        services.AddScoped<Bm25RetrieverService>();
        services.AddScoped<IRetriever, HybridRagRetrieverService>();
        services.AddScoped<IKnowledgeGraphProvider, LightweightKnowledgeGraphProvider>();
        services.AddScoped<IPromptGuard, PromptGuardService>();
        services.AddScoped<IPiiDetector, PiiDetectorService>();
        services.AddScoped<IReranker, CrossEncoderRerankerAdapter>();
        services.AddScoped<IEvidenceScorer, RagEvidenceScorerService>();
        services.AddScoped<IAnswerSynthesizer, RagAnswerSynthesizerService>();
        services.AddScoped<IVerificationEngine, RagVerificationEngineService>();
        services.AddScoped<IRetrievalPipeline, RagRetrievalPipelineService>();
        services.AddScoped<IRagExperimentService, RagExperimentService>();
        services.AddScoped<LlmUsageRecordRepository>();
        services.AddScoped<IMeteringService, MeteringService>();
        services.AddScoped<IRagRetrievalService, RagRetrievalService>();
        services.AddScoped<BM25RetrievalService>();
        services.AddScoped<HybridRetrievalService>();
        services.AddScoped<CrossEncoderRerankerService>();
        services.AddScoped<ContextCompressionService>();
        services.AddScoped<FreshnessBoostService>();

        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IChunkService, ChunkService>();
        services.AddSingleton<IDocumentParseStrategy, DocumentParseStrategyService>();
        services.AddScoped<DocumentProcessingService>();

        // v5 §32-44 知识库专题仓储
        services.AddScoped<KnowledgeBaseMetaRepository>();
        services.AddScoped<KnowledgeDocumentMetaRepository>();
        services.AddScoped<KnowledgeBaseVersionRepository>();
        services.AddScoped<KnowledgeJobRepository>();
        services.AddScoped<KnowledgeBaseBindingRepository>();
        services.AddScoped<KnowledgeBasePermissionRepository>();
        services.AddScoped<KnowledgeRetrievalLogRepository>();
        services.AddScoped<KnowledgeProviderConfigRepository>();
        services.AddScoped<KnowledgeTableColumnRepository>();
        services.AddScoped<KnowledgeTableRowRepository>();
        services.AddScoped<KnowledgeImageItemRepository>();
        services.AddScoped<KnowledgeImageAnnotationRepository>();

        // v5 §32-44 知识库专题应用服务（M9 任务系统 + M10 检索日志 + M11 治理服务全集）
        services.AddScoped<KnowledgeJobService>();
        services.AddScoped<IKnowledgeJobService>(sp => sp.GetRequiredService<KnowledgeJobService>());
        services.AddScoped<IRetrievalLogService, RetrievalLogService>();
        services.AddScoped<IKnowledgeBindingService, KnowledgeBindingService>();
        services.AddScoped<IKnowledgePermissionService, KnowledgePermissionService>();
        services.AddScoped<IKnowledgeVersionService, KnowledgeVersionService>();
        services.AddScoped<IKnowledgeProviderConfigService, KnowledgeProviderConfigService>();
        services.AddScoped<IKnowledgeTableViewService, KnowledgeTableViewService>();
        services.AddScoped<IKnowledgeImageItemService, KnowledgeImageItemService>();

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

        services.AddSingleton<IOpenApiPluginParser, OpenApiPluginParser>();

        services.AddTransient<DirectPythonExecutor>();
        services.AddTransient<DockerPythonExecutor>();
        services.AddTransient<SandboxedPythonExecutor>();
        services.AddScoped<ICodeExecutionService>(sp =>
            sp.GetRequiredService<SandboxedPythonExecutor>());

        return services;
    }
}
