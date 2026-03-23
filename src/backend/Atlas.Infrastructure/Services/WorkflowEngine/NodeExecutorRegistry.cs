using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine;

/// <summary>
/// 节点执行器注册表——按作用域构建，汇总当前作用域的 <see cref="INodeExecutor"/> 实现。
/// </summary>
public sealed class NodeExecutorRegistry
{
    private readonly Dictionary<WorkflowNodeType, INodeExecutor> _executors;
    private readonly List<NodeTypeMetadata> _metadata;

    public NodeExecutorRegistry(IEnumerable<INodeExecutor> executors)
    {
        _executors = new Dictionary<WorkflowNodeType, INodeExecutor>();
        _metadata = new List<NodeTypeMetadata>();

        foreach (var executor in executors)
        {
            _executors[executor.NodeType] = executor;
            _metadata.Add(BuildMetadata(executor.NodeType));
        }
    }

    public INodeExecutor? GetExecutor(WorkflowNodeType type)
    {
        return _executors.GetValueOrDefault(type);
    }

    public IReadOnlyList<NodeTypeMetadata> GetAllTypes() => _metadata;

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
}
