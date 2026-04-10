using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine;

internal sealed class StaticWorkflowNodeDeclaration : IWorkflowNodeDeclaration
{
    public required WorkflowNodeType Type { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string Category { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<WorkflowNodePortMetadata> Ports { get; init; }

    public required string ConfigSchemaJson { get; init; }

    public required WorkflowNodeUiMetadata UiMeta { get; init; }
}

internal static class BuiltInWorkflowNodeDeclarations
{
    public static readonly IReadOnlyList<IWorkflowNodeDeclaration> All =
    [
        Create(WorkflowNodeType.Entry, "Entry", "开始", "flow", "工作流入口。", [Out("output")], "#6366F1"),
        Create(WorkflowNodeType.Exit, "Exit", "结束", "flow", "工作流结束并返回结果。", [In("input")], "#6366F1"),
        Create(WorkflowNodeType.Selector, "If", "条件判断", "flow", "根据条件选择分支。", [In("input"), Out("true"), Out("false"), Out("else")], "#6366F1"),
        Create(WorkflowNodeType.Loop, "Loop", "循环", "flow", "循环执行子流程。", [In("input"), Out("body"), Out("continue"), Out("done")], "#6366F1", true),
        Create(WorkflowNodeType.Batch, "Batch", "批处理", "flow", "并发批处理子流程。", [In("input"), Out("output")], "#6366F1", true),
        Create(WorkflowNodeType.Break, "Break", "中断循环", "flow", "中断当前循环。", [In("input"), Out("output")], "#6366F1"),
        Create(WorkflowNodeType.Continue, "Continue", "继续循环", "flow", "跳过当前迭代后续节点。", [In("input"), Out("output")], "#6366F1"),

        Create(WorkflowNodeType.Llm, "Llm", "大模型", "ai", "调用大模型进行推理。", [In("input"), Out("output")], "#8B5CF6"),
        Create(WorkflowNodeType.IntentDetector, "IntentDetector", "意图识别", "ai", "识别用户意图并输出分类。", [In("input"), Out("output")], "#8B5CF6"),
        Create(WorkflowNodeType.QuestionAnswer, "QuestionAnswer", "问答", "ai", "向用户提问并收集答案。", [In("input"), Out("output")], "#8B5CF6"),

        Create(WorkflowNodeType.CodeRunner, "CodeRunner", "代码执行", "data", "执行脚本进行数据处理。", [In("input"), Out("output")], "#06B6D4"),
        Create(WorkflowNodeType.TextProcessor, "TextProcessor", "文本处理", "data", "模板渲染或字符串处理。", [In("input"), Out("output")], "#06B6D4"),
        Create(WorkflowNodeType.JsonSerialization, "JsonSerialization", "JSON序列化", "data", "对象转 JSON。", [In("input"), Out("output")], "#06B6D4"),
        Create(WorkflowNodeType.JsonDeserialization, "JsonDeserialization", "JSON反序列化", "data", "JSON 转对象。", [In("input"), Out("output")], "#06B6D4"),
        Create(WorkflowNodeType.VariableAggregator, "VariableAggregator", "变量聚合", "data", "聚合多个变量。", [In("input"), Out("output")], "#06B6D4"),
        Create(WorkflowNodeType.AssignVariable, "AssignVariable", "变量赋值", "data", "设置变量值。", [In("input"), Out("output")], "#06B6D4"),
        Create(WorkflowNodeType.VariableAssignerWithinLoop, "VariableAssignerWithinLoop", "循环内变量赋值", "data", "循环作用域内赋值。", [In("input"), Out("output")], "#06B6D4"),

        Create(WorkflowNodeType.Plugin, "Plugin", "插件", "external", "调用插件工具。", [In("input"), Out("output")], "#F59E0B"),
        Create(WorkflowNodeType.HttpRequester, "HttpRequester", "HTTP请求", "external", "发起 HTTP 请求。", [In("input"), Out("output")], "#F59E0B"),
        Create(WorkflowNodeType.SubWorkflow, "SubWorkflow", "子工作流", "external", "调用其它工作流。", [In("input"), Out("output")], "#F59E0B"),

        Create(WorkflowNodeType.KnowledgeRetriever, "KnowledgeRetriever", "知识库检索", "knowledge", "检索知识片段。", [In("input"), Out("output")], "#10B981"),
        Create(WorkflowNodeType.KnowledgeIndexer, "KnowledgeIndexer", "知识库写入", "knowledge", "写入文档到知识库。", [In("input"), Out("output")], "#10B981"),
        Create(WorkflowNodeType.KnowledgeDeleter, "KnowledgeDeleter", "知识库删除", "knowledge", "删除知识库文档。", [In("input"), Out("output")], "#10B981"),
        Create(WorkflowNodeType.Ltm, "Ltm", "长期记忆", "knowledge", "读写长期记忆。", [In("input"), Out("output")], "#10B981"),

        Create(WorkflowNodeType.DatabaseQuery, "DatabaseQuery", "数据库查询", "database", "查询数据库记录。", [In("input"), Out("output")], "#3B82F6"),
        Create(WorkflowNodeType.DatabaseInsert, "DatabaseInsert", "数据库新增", "database", "插入数据库记录。", [In("input"), Out("output")], "#3B82F6"),
        Create(WorkflowNodeType.DatabaseUpdate, "DatabaseUpdate", "数据库更新", "database", "更新数据库记录。", [In("input"), Out("output")], "#3B82F6"),
        Create(WorkflowNodeType.DatabaseDelete, "DatabaseDelete", "数据库删除", "database", "删除数据库记录。", [In("input"), Out("output")], "#3B82F6"),
        Create(WorkflowNodeType.DatabaseCustomSql, "DatabaseCustomSql", "自定义SQL", "database", "执行自定义 SQL。", [In("input"), Out("output")], "#3B82F6"),

        Create(WorkflowNodeType.CreateConversation, "CreateConversation", "创建会话", "conversation", "创建新会话。", [In("input"), Out("conversation_id")], "#EC4899"),
        Create(WorkflowNodeType.ConversationList, "ConversationList", "查询会话列表", "conversation", "查询会话列表。", [In("input"), Out("conversations")], "#EC4899"),
        Create(WorkflowNodeType.ConversationUpdate, "ConversationUpdate", "修改会话", "conversation", "修改会话信息。", [In("input"), Out("output")], "#EC4899"),
        Create(WorkflowNodeType.ConversationDelete, "ConversationDelete", "删除会话", "conversation", "删除指定会话。", [In("input"), Out("output")], "#EC4899"),
        Create(WorkflowNodeType.ConversationHistory, "ConversationHistory", "会话历史", "conversation", "读取会话历史消息。", [In("input"), Out("messages")], "#EC4899"),
        Create(WorkflowNodeType.ClearConversationHistory, "ClearConversationHistory", "清空会话历史", "conversation", "清空会话历史上下文。", [In("input"), Out("output")], "#EC4899"),
        Create(WorkflowNodeType.MessageList, "MessageList", "查询消息列表", "conversation", "查询消息列表。", [In("input"), Out("messages")], "#EC4899"),
        Create(WorkflowNodeType.CreateMessage, "CreateMessage", "创建消息", "conversation", "创建会话消息。", [In("input"), Out("message_id")], "#EC4899"),
        Create(WorkflowNodeType.EditMessage, "EditMessage", "修改消息", "conversation", "编辑会话消息。", [In("input"), Out("output")], "#EC4899"),
        Create(WorkflowNodeType.DeleteMessage, "DeleteMessage", "删除消息", "conversation", "删除会话消息。", [In("input"), Out("output")], "#EC4899"),

        Create(WorkflowNodeType.OutputEmitter, "OutputEmitter", "中间输出", "io", "输出中间文本或数据。", [In("input"), Out("output")], "#EF4444"),
        Create(WorkflowNodeType.InputReceiver, "InputReceiver", "等待输入", "io", "中断流程并等待用户输入。", [Out("output")], "#EF4444"),

        Create(WorkflowNodeType.Agent, "Agent", "Agent", "ai", "调用 Agent 能力。", [In("input"), Out("output")], "#8B5CF6"),
        Create(WorkflowNodeType.Comment, "Comment", "注释", "flow", "画布注释节点。", [In("input"), Out("output")], "#64748B")
    ];

    private static IWorkflowNodeDeclaration Create(
        WorkflowNodeType type,
        string key,
        string name,
        string category,
        string description,
        IReadOnlyList<WorkflowNodePortMetadata> ports,
        string color,
        bool supportsBatch = false)
    {
        return new StaticWorkflowNodeDeclaration
        {
            Type = type,
            Key = key,
            Name = name,
            Category = category,
            Description = description,
            Ports = ports,
            ConfigSchemaJson = """
                               {
                                 "type": "object",
                                 "additionalProperties": true
                               }
                               """,
            UiMeta = new WorkflowNodeUiMetadata(
                Icon: $"workflow/{key}.svg",
                Color: color,
                SupportsBatch: supportsBatch)
        };
    }

    private static WorkflowNodePortMetadata In(string key, string dataType = "any", bool required = false, int maxConnections = 1)
        => new(key, key, WorkflowNodePortDirection.Input, dataType, required, maxConnections);

    private static WorkflowNodePortMetadata Out(string key, string dataType = "any", bool required = false, int maxConnections = 1)
        => new(key, key, WorkflowNodePortDirection.Output, dataType, required, maxConnections);
}
