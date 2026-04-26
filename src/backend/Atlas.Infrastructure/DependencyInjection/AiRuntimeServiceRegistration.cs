using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Application.AgentTeam.Abstractions;
using Atlas.Core.Expressions;
using Atlas.Infrastructure.LogicFlow.Expressions;
using Atlas.Infrastructure.LogicFlow.Expressions.Functions;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;
using Atlas.Infrastructure.Services.WorkflowEngine;
using Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;
using Atlas.Infrastructure.Services.AgentTeam;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Infrastructure.DependencyInjection;

public static class AiRuntimeServiceRegistration
{
    /// <summary>
    /// AI 应用运行态层：Agent 对话、会话管理、多 Agent 编排运行态、
    /// 多模态调用、记忆服务、工作流执行。AppHost 需注册。
    /// </summary>
    public static IServiceCollection AddAiRuntimeInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ConversationRepository>();
        services.AddScoped<ChatMessageRepository>();
        services.AddScoped<AgenticRagRunHistoryRepository>();
        services.AddScoped<TeamAgentConversationRepository>();
        services.AddScoped<TeamAgentMessageRepository>();
        services.AddScoped<TeamAgentExecutionRepository>();
        services.AddScoped<TeamAgentExecutionStepRepository>();
        services.AddScoped<LongTermMemoryRepository>();
        services.AddScoped<MultiAgentOrchestrationRepository>();
        services.AddScoped<MultiAgentExecutionRepository>();
        services.AddScoped<MultimodalAssetRepository>();
        services.AddScoped<ExecutionRunRepository>();
        services.AddScoped<NodeRunRepository>();
        services.AddScoped<IExecutionRunRepository>(sp => sp.GetRequiredService<ExecutionRunRepository>());
        services.AddScoped<INodeRunRepository>(sp => sp.GetRequiredService<NodeRunRepository>());

        services.AddScoped<IConversationOwnerResolver, ConversationOwnerResolver>();
        services.AddScoped<IAgentChatService, AgentChatService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IMultiAgentOrchestrationService, MultiAgentOrchestrationService>();
        services.AddScoped<IMultimodalService, MultimodalService>();
        services.AddScoped<IAiMemoryService, AiMemoryService>();
        services.AddScoped<IMemoryProvider, SqlMemoryProvider>();
        services.AddScoped<ILongTermMemoryExtractionService, LongTermMemoryExtractionService>();
        services.AddScoped<IOrchestrationCompiler, OrchestrationCompiler>();
        services.AddScoped<IOrchestrationExecutor, OrchestrationExecutor>();
        services.AddScoped<IOrchestrationCompensationService, OrchestrationCompensationService>();
        services.AddScoped<IAiSearchService, AiSearchService>();
        services.AddScoped<IAgentOrchestrator, RagAgentOrchestratorService>();
        services.AddScoped<IAgenticRagOrchestrationService, AgenticRagOrchestrationService>();
        services.AddScoped<ITeamAgentOrchestrationRuntime, FrameworkAwareTeamAgentOrchestrationRuntime>();
        services.AddScoped<IAiWorkflowExecutionService, AiWorkflowExecutionService>();
        services.AddSingleton<MultiAgentExecutionTracker>();

        // Workflow V2 Execution Engine
        services.TryAddSingleton<IAstCache>(sp => new AstCompilationCache(4096));
        services.TryAddSingleton<IFunctionRegistry, BuiltinFunctionRegistry>();
        services.TryAddSingleton<ExprEvaluator>();
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
        services.AddScoped<INodeExecutor, IntentDetectorNodeExecutor>();
        services.AddScoped<INodeExecutor, QuestionAnswerNodeExecutor>();
        services.AddScoped<INodeExecutor, AgentNodeExecutor>();
        services.AddScoped<INodeExecutor, PluginNodeExecutor>();
        services.AddScoped<INodeExecutor, SubWorkflowNodeExecutor>();
        services.AddScoped<INodeExecutor, LoopNodeExecutor>();
        services.AddScoped<INodeExecutor, BatchNodeExecutor>();
        services.AddScoped<INodeExecutor, BreakNodeExecutor>();
        services.AddScoped<INodeExecutor, ContinueNodeExecutor>();
        services.AddScoped<INodeExecutor, CodeRunnerNodeExecutor>();
        services.AddScoped<INodeExecutor, HttpRequesterNodeExecutor>();
        services.AddScoped<INodeExecutor, TextProcessorNodeExecutor>();
        services.AddScoped<INodeExecutor, KnowledgeRetrieverNodeExecutor>();
        services.AddScoped<INodeExecutor, KnowledgeIndexerNodeExecutor>();
        services.AddScoped<INodeExecutor, LtmNodeExecutor>();
        services.AddScoped<INodeExecutor, DatabaseQueryNodeExecutor>();
        services.AddScoped<INodeExecutor, DatabaseInsertNodeExecutor>();
        services.AddScoped<INodeExecutor, DatabaseUpdateNodeExecutor>();
        services.AddScoped<INodeExecutor, DatabaseDeleteNodeExecutor>();
        services.AddScoped<INodeExecutor, DatabaseCustomSqlNodeExecutor>();
        services.AddScoped<INodeExecutor, CreateConversationNodeExecutor>();
        services.AddScoped<INodeExecutor, ConversationListNodeExecutor>();
        services.AddScoped<INodeExecutor, ConversationUpdateNodeExecutor>();
        services.AddScoped<INodeExecutor, ConversationDeleteNodeExecutor>();
        services.AddScoped<INodeExecutor, ClearConversationHistoryNodeExecutor>();
        services.AddScoped<INodeExecutor, ConversationHistoryNodeExecutor>();
        services.AddScoped<INodeExecutor, MessageListNodeExecutor>();
        services.AddScoped<INodeExecutor, CreateMessageNodeExecutor>();
        services.AddScoped<INodeExecutor, EditMessageNodeExecutor>();
        services.AddScoped<INodeExecutor, DeleteMessageNodeExecutor>();
        services.AddScoped<INodeExecutor, OutputEmitterNodeExecutor>();
        services.AddScoped<INodeExecutor, InputReceiverNodeExecutor>();
        services.AddScoped<INodeExecutor, AssignVariableNodeExecutor>();
        services.AddScoped<INodeExecutor, VariableAssignerWithinLoopNodeExecutor>();
        services.AddScoped<INodeExecutor, VariableAggregatorNodeExecutor>();
        services.AddScoped<INodeExecutor, JsonSerializationNodeExecutor>();
        services.AddScoped<INodeExecutor, JsonDeserializationNodeExecutor>();
        services.AddScoped<INodeExecutor, KnowledgeDeleterNodeExecutor>();

        services.AddHttpClient("WorkflowEngine", client => client.Timeout = TimeSpan.FromSeconds(30));

        services.AddWorkspaceChannelInfrastructure();

        return services;
    }
}
