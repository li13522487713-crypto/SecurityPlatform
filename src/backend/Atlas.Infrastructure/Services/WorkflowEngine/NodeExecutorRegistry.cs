using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Atlas.Infrastructure.Services.WorkflowEngine;

/// <summary>
/// 节点执行器注册表——按作用域构建，汇总当前作用域的 <see cref="INodeExecutor"/> 实现。
/// </summary>
public sealed class NodeExecutorRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<WorkflowNodeType, INodeExecutor> _executors;
    private readonly Dictionary<WorkflowNodeType, Type> _executorTypes;
    private readonly List<NodeTypeMetadata> _metadata;
    private readonly Dictionary<WorkflowNodeType, IWorkflowNodeDeclaration> _declarations;

    public NodeExecutorRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _executors = new ConcurrentDictionary<WorkflowNodeType, INodeExecutor>();
        _executorTypes = new Dictionary<WorkflowNodeType, Type>
        {
            [WorkflowNodeType.Entry] = typeof(EntryNodeExecutor),
            [WorkflowNodeType.Exit] = typeof(ExitNodeExecutor),
            [WorkflowNodeType.Selector] = typeof(SelectorNodeExecutor),
            [WorkflowNodeType.Llm] = typeof(LlmNodeExecutor),
            [WorkflowNodeType.IntentDetector] = typeof(IntentDetectorNodeExecutor),
            [WorkflowNodeType.QuestionAnswer] = typeof(QuestionAnswerNodeExecutor),
            [WorkflowNodeType.Agent] = typeof(AgentNodeExecutor),
            [WorkflowNodeType.Plugin] = typeof(PluginNodeExecutor),
            [WorkflowNodeType.SubWorkflow] = typeof(SubWorkflowNodeExecutor),
            [WorkflowNodeType.Loop] = typeof(LoopNodeExecutor),
            [WorkflowNodeType.Batch] = typeof(BatchNodeExecutor),
            [WorkflowNodeType.Break] = typeof(BreakNodeExecutor),
            [WorkflowNodeType.Continue] = typeof(ContinueNodeExecutor),
            [WorkflowNodeType.CodeRunner] = typeof(CodeRunnerNodeExecutor),
            [WorkflowNodeType.HttpRequester] = typeof(HttpRequesterNodeExecutor),
            [WorkflowNodeType.TextProcessor] = typeof(TextProcessorNodeExecutor),
            [WorkflowNodeType.KnowledgeRetriever] = typeof(KnowledgeRetrieverNodeExecutor),
            [WorkflowNodeType.KnowledgeIndexer] = typeof(KnowledgeIndexerNodeExecutor),
            [WorkflowNodeType.Ltm] = typeof(LtmNodeExecutor),
            [WorkflowNodeType.DatabaseQuery] = typeof(DatabaseQueryNodeExecutor),
            [WorkflowNodeType.DatabaseInsert] = typeof(DatabaseInsertNodeExecutor),
            [WorkflowNodeType.DatabaseUpdate] = typeof(DatabaseUpdateNodeExecutor),
            [WorkflowNodeType.DatabaseDelete] = typeof(DatabaseDeleteNodeExecutor),
            [WorkflowNodeType.DatabaseCustomSql] = typeof(DatabaseCustomSqlNodeExecutor),
            [WorkflowNodeType.DatabaseNl2Sql] = typeof(DatabaseNl2SqlNodeExecutor),
            [WorkflowNodeType.CreateConversation] = typeof(CreateConversationNodeExecutor),
            [WorkflowNodeType.ConversationList] = typeof(ConversationListNodeExecutor),
            [WorkflowNodeType.ConversationUpdate] = typeof(ConversationUpdateNodeExecutor),
            [WorkflowNodeType.ConversationDelete] = typeof(ConversationDeleteNodeExecutor),
            [WorkflowNodeType.ClearConversationHistory] = typeof(ClearConversationHistoryNodeExecutor),
            [WorkflowNodeType.ConversationHistory] = typeof(ConversationHistoryNodeExecutor),
            [WorkflowNodeType.MessageList] = typeof(MessageListNodeExecutor),
            [WorkflowNodeType.CreateMessage] = typeof(CreateMessageNodeExecutor),
            [WorkflowNodeType.EditMessage] = typeof(EditMessageNodeExecutor),
            [WorkflowNodeType.DeleteMessage] = typeof(DeleteMessageNodeExecutor),
            [WorkflowNodeType.OutputEmitter] = typeof(OutputEmitterNodeExecutor),
            [WorkflowNodeType.InputReceiver] = typeof(InputReceiverNodeExecutor),
            [WorkflowNodeType.AssignVariable] = typeof(AssignVariableNodeExecutor),
            [WorkflowNodeType.VariableAssignerWithinLoop] = typeof(VariableAssignerWithinLoopNodeExecutor),
            [WorkflowNodeType.VariableAggregator] = typeof(VariableAggregatorNodeExecutor),
            [WorkflowNodeType.JsonSerialization] = typeof(JsonSerializationNodeExecutor),
            [WorkflowNodeType.JsonDeserialization] = typeof(JsonDeserializationNodeExecutor),
            [WorkflowNodeType.KnowledgeDeleter] = typeof(KnowledgeDeleterNodeExecutor)
        };
        _metadata = new List<NodeTypeMetadata>();
        _declarations = BuiltInWorkflowNodeDeclarations.All
            .GroupBy(x => x.Type)
            .Select(x => x.Last())
            .ToDictionary(x => x.Type, x => x);

        foreach (var declaration in _declarations.Values)
        {
            _metadata.Add(ToMetadata(declaration));
        }

        foreach (var registeredType in _executorTypes.Keys)
        {
            if (!_declarations.ContainsKey(registeredType))
            {
                _metadata.Add(BuildMetadata(registeredType));
            }
        }
    }

    // 为集成测试保留可注入执行器构造方式。
    public NodeExecutorRegistry(params INodeExecutor[] executors)
        : this(new ServiceCollection().BuildServiceProvider())
    {
        foreach (var executor in executors)
        {
            _executors[executor.NodeType] = executor;
            _executorTypes[executor.NodeType] = executor.GetType();
        }
    }

    public INodeExecutor? GetExecutor(WorkflowNodeType type)
    {
        if (!_executorTypes.TryGetValue(type, out var executorType))
        {
            return null;
        }

        return _executors.GetOrAdd(type, static (_, state) =>
        {
            var instance = ActivatorUtilities.CreateInstance(state.Provider, state.ExecutorType);
            if (instance is not INodeExecutor executor)
            {
                throw new InvalidOperationException($"节点执行器类型 {state.ExecutorType.FullName} 未实现 INodeExecutor。");
            }

            return executor;
        }, (Provider: _serviceProvider, ExecutorType: executorType));
    }

    public IReadOnlyList<NodeTypeMetadata> GetAllTypes() => _metadata;

    public IWorkflowNodeDeclaration? GetDeclaration(WorkflowNodeType type)
    {
        return _declarations.GetValueOrDefault(type);
    }

    private static NodeTypeMetadata BuildMetadata(WorkflowNodeType type)
    {
        var (name, category, description) = type switch
        {
            WorkflowNodeType.Entry => ("开始", "Flow", "工作流入口节点"),
            WorkflowNodeType.Exit => ("结束", "Flow", "工作流出口节点"),
            WorkflowNodeType.Llm => ("大模型", "AI", "调用大语言模型"),
            WorkflowNodeType.Agent => ("Agent", "AI", "调用 Agent 进行对话推理"),
            WorkflowNodeType.Plugin => ("插件", "Integration", "调用外部插件或 API"),
            WorkflowNodeType.CodeRunner => ("代码执行", "Compute", "运行代码或脚本"),
            WorkflowNodeType.KnowledgeRetriever => ("知识检索", "RAG", "检索知识库内容"),
            WorkflowNodeType.Selector => ("条件分支", "Flow", "根据条件选择分支"),
            WorkflowNodeType.SubWorkflow => ("子工作流", "Flow", "调用另一个工作流"),
            WorkflowNodeType.TextProcessor => ("文本处理", "Transform", "文本模板渲染与处理"),
            WorkflowNodeType.OutputEmitter => ("输出", "Output", "输出流程结果"),
            WorkflowNodeType.Loop => ("循环", "Flow", "循环执行子节点"),
            WorkflowNodeType.HttpRequester => ("HTTP 请求", "Integration", "发起 HTTP 请求"),
            WorkflowNodeType.AssignVariable => ("变量赋值", "Data", "设置变量值"),
            WorkflowNodeType.VariableAggregator => ("变量聚合", "Data", "聚合多个变量"),
            WorkflowNodeType.DatabaseQuery => ("数据库查询", "Data", "查询数据库"),
            WorkflowNodeType.JsonSerialization => ("JSON 序列化", "Transform", "将对象序列化为 JSON"),
            WorkflowNodeType.JsonDeserialization => ("JSON 反序列化", "Transform", "将 JSON 反序列化为对象"),
            _ => (type.ToString(), "Other", $"节点类型: {type}")
        };

        return new NodeTypeMetadata(type, type.ToString(), name, category, description);
    }

    private static NodeTypeMetadata ToMetadata(IWorkflowNodeDeclaration declaration)
    {
        return new NodeTypeMetadata(
            declaration.Type,
            declaration.Key,
            declaration.Name,
            declaration.Category,
            declaration.Description);
    }
}
