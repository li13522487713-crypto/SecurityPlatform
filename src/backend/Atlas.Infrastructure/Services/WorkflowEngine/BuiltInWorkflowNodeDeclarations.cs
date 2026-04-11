using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using System.Text;

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
            ConfigSchemaJson = BuildConfigSchema(type),
            UiMeta = new WorkflowNodeUiMetadata(
                Icon: $"workflow/{key}.svg",
                Color: color,
                SupportsBatch: supportsBatch)
        };
    }

    private static string BuildConfigSchema(WorkflowNodeType type)
    {
        return type switch
        {
            WorkflowNodeType.Entry => BuildObjectSchema(["entryVariable"], new Dictionary<string, string>
            {
                ["entryVariable"] = "string",
                ["entryDescription"] = "string",
                ["entryAutoSaveHistory"] = "boolean"
            }),
            WorkflowNodeType.Exit => BuildObjectSchema(["exitTerminateMode"], new Dictionary<string, string>
            {
                ["exitTerminateMode"] = "string",
                ["exitTemplate"] = "string",
                ["exitStreaming"] = "boolean"
            }),
            WorkflowNodeType.Selector => BuildObjectSchema([], new Dictionary<string, string>
            {
                ["condition"] = "string",
                ["logic"] = "string",
                ["conditions"] = "array"
            }),
            WorkflowNodeType.Loop => BuildObjectSchema(["mode", "maxIterations"], new Dictionary<string, string>
            {
                ["mode"] = "string",
                ["maxIterations"] = "number",
                ["collectionPath"] = "string",
                ["condition"] = "string",
                ["itemVariable"] = "string",
                ["itemIndexVariable"] = "string",
                ["bodyNodeKeys"] = "string"
            }),
            WorkflowNodeType.Batch => BuildObjectSchema(["concurrentSize", "batchSize", "inputArrayPath"], new Dictionary<string, string>
            {
                ["concurrentSize"] = "number",
                ["batchSize"] = "number",
                ["inputArrayPath"] = "string",
                ["itemVariable"] = "string",
                ["itemIndexVariable"] = "string",
                ["outputKey"] = "string"
            }),
            WorkflowNodeType.Break => BuildObjectSchema(["reason"]),
            WorkflowNodeType.Continue => BuildObjectSchema([], new Dictionary<string, string>
            {
                ["remark"] = "string"
            }),
            WorkflowNodeType.Llm => BuildObjectSchema(["provider", "model", "prompt"], new Dictionary<string, string>
            {
                ["provider"] = "string",
                ["model"] = "string",
                ["prompt"] = "string",
                ["systemPrompt"] = "string",
                ["temperature"] = "number",
                ["maxTokens"] = "number",
                ["stream"] = "boolean",
                ["outputKey"] = "string"
            }),
            WorkflowNodeType.Agent => BuildObjectSchema(["agentId", "message"], new Dictionary<string, string>
            {
                ["agentId"] = "string",
                ["message"] = "string",
                ["conversationId"] = "string",
                ["userId"] = "string",
                ["enableRag"] = "boolean",
                ["outputKey"] = "string"
            }),
            WorkflowNodeType.IntentDetector => BuildObjectSchema(["input", "model"], new Dictionary<string, string>
            {
                ["input"] = "string",
                ["provider"] = "string",
                ["model"] = "string",
                ["systemPrompt"] = "string",
                ["temperature"] = "number",
                ["intents"] = "array"
            }),
            WorkflowNodeType.QuestionAnswer => BuildObjectSchema(["question", "answerPath"], new Dictionary<string, string>
            {
                ["question"] = "string",
                ["answerPath"] = "string",
                ["answerType"] = "string",
                ["fixedChoices"] = "array",
                ["maxAnswerCount"] = "number"
            }),
            WorkflowNodeType.CodeRunner => BuildObjectSchema(["code"], new Dictionary<string, string>
            {
                ["language"] = "string",
                ["code"] = "string",
                ["outputKey"] = "string"
            }),
            WorkflowNodeType.TextProcessor => BuildObjectSchema(["template", "outputKey"]),
            WorkflowNodeType.JsonSerialization => BuildObjectSchema(["variableKeys", "outputKey"], new Dictionary<string, string>
            {
                ["variableKeys"] = "array",
                ["outputKey"] = "string"
            }),
            WorkflowNodeType.JsonDeserialization => BuildObjectSchema(["inputVariable"]),
            WorkflowNodeType.VariableAggregator => BuildObjectSchema(["variableKeys", "outputKey"], new Dictionary<string, string>
            {
                ["variableKeys"] = "array",
                ["outputKey"] = "string"
            }),
            WorkflowNodeType.AssignVariable => BuildObjectSchema(["assignments"]),
            WorkflowNodeType.VariableAssignerWithinLoop => BuildObjectSchema(["assignments"]),
            WorkflowNodeType.Plugin => BuildObjectSchema(["pluginId", "apiId"], new Dictionary<string, string>
            {
                ["pluginId"] = "string",
                ["apiId"] = "string",
                ["inputJson"] = "object",
                ["outputKey"] = "string"
            }),
            WorkflowNodeType.HttpRequester => BuildObjectSchema(["url", "method"], new Dictionary<string, string>
            {
                ["url"] = "string",
                ["method"] = "string",
                ["headers"] = "object",
                ["body"] = "object",
                ["timeout"] = "number",
                ["retryTimes"] = "number"
            }),
            WorkflowNodeType.SubWorkflow => BuildObjectSchema(["workflowId"], new Dictionary<string, string>
            {
                ["workflowId"] = "string",
                ["maxDepth"] = "number",
                ["inheritVariables"] = "boolean",
                ["mergeOutputs"] = "boolean",
                ["inputsVariable"] = "string",
                ["outputKey"] = "string"
            }),
            WorkflowNodeType.InputReceiver => BuildObjectSchema(["inputPath"], new Dictionary<string, string>
            {
                ["inputPath"] = "string",
                ["outputSchema"] = "object"
            }),
            WorkflowNodeType.OutputEmitter => BuildObjectSchema(["outputKey"], new Dictionary<string, string>
            {
                ["outputKey"] = "string",
                ["template"] = "string"
            }),
            WorkflowNodeType.KnowledgeRetriever => BuildObjectSchema(["knowledgeIds", "query"], new Dictionary<string, string>
            {
                ["knowledgeIds"] = "array",
                ["query"] = "string",
                ["topK"] = "number",
                ["minScore"] = "number"
            }),
            WorkflowNodeType.KnowledgeIndexer => BuildObjectSchema(["knowledgeId", "fileId"], new Dictionary<string, string>
            {
                ["knowledgeId"] = "number",
                ["fileId"] = "number",
                ["fileName"] = "string",
                ["contentType"] = "string",
                ["fileSizeBytes"] = "number",
                ["chunkSize"] = "number",
                ["overlap"] = "number"
            }),
            WorkflowNodeType.KnowledgeDeleter => BuildObjectSchema(["knowledgeId"], new Dictionary<string, string>
            {
                ["knowledgeId"] = "number",
                ["documentId"] = "number"
            }),
            WorkflowNodeType.Ltm => BuildObjectSchema(["action", "userId"], new Dictionary<string, string>
            {
                ["action"] = "string",
                ["userId"] = "number",
                ["agentId"] = "number",
                ["conversationId"] = "number",
                ["memoryKey"] = "string",
                ["content"] = "string",
                ["source"] = "string",
                ["limit"] = "number",
                ["memoryId"] = "number"
            }),
            WorkflowNodeType.DatabaseQuery => BuildObjectSchema(["databaseInfoId"], new Dictionary<string, string>
            {
                ["databaseInfoId"] = "number",
                ["queryFields"] = "array",
                ["clauseGroup"] = "array",
                ["limit"] = "number",
                ["outputKey"] = "string"
            }),
            WorkflowNodeType.DatabaseInsert => BuildObjectSchema(["databaseInfoId"], new Dictionary<string, string>
            {
                ["databaseInfoId"] = "number",
                ["rows"] = "array"
            }),
            WorkflowNodeType.DatabaseUpdate => BuildObjectSchema(["databaseInfoId"], new Dictionary<string, string>
            {
                ["databaseInfoId"] = "number",
                ["updateFields"] = "object",
                ["clauseGroup"] = "array"
            }),
            WorkflowNodeType.DatabaseDelete => BuildObjectSchema(["databaseInfoId"], new Dictionary<string, string>
            {
                ["databaseInfoId"] = "number",
                ["clauseGroup"] = "array"
            }),
            WorkflowNodeType.DatabaseCustomSql => BuildObjectSchema(["databaseInfoId", "sqlTemplate"], new Dictionary<string, string>
            {
                ["databaseInfoId"] = "number",
                ["sqlTemplate"] = "string"
            }),
            WorkflowNodeType.CreateConversation => BuildObjectSchema(["userId", "agentId"], new Dictionary<string, string>
            {
                ["title"] = "string",
                ["userId"] = "number",
                ["agentId"] = "number"
            }),
            WorkflowNodeType.ConversationList => BuildObjectSchema(["userId", "agentId"], new Dictionary<string, string>
            {
                ["userId"] = "number",
                ["agentId"] = "number",
                ["pageIndex"] = "number",
                ["pageSize"] = "number"
            }),
            WorkflowNodeType.ConversationUpdate => BuildObjectSchema(["userId", "conversationId", "title"], new Dictionary<string, string>
            {
                ["userId"] = "number",
                ["conversationId"] = "number",
                ["title"] = "string"
            }),
            WorkflowNodeType.ConversationDelete => BuildObjectSchema(["userId", "conversationId"], new Dictionary<string, string>
            {
                ["userId"] = "number",
                ["conversationId"] = "number"
            }),
            WorkflowNodeType.ConversationHistory => BuildObjectSchema(["userId", "conversationId"], new Dictionary<string, string>
            {
                ["userId"] = "number",
                ["conversationId"] = "number",
                ["limit"] = "number",
                ["includeContextMarkers"] = "boolean"
            }),
            WorkflowNodeType.ClearConversationHistory => BuildObjectSchema(["userId", "conversationId"], new Dictionary<string, string>
            {
                ["userId"] = "number",
                ["conversationId"] = "number"
            }),
            WorkflowNodeType.MessageList => BuildObjectSchema(["userId", "conversationId"], new Dictionary<string, string>
            {
                ["userId"] = "number",
                ["conversationId"] = "number",
                ["pageIndex"] = "number",
                ["pageSize"] = "number"
            }),
            WorkflowNodeType.CreateMessage => BuildObjectSchema(["conversationId", "content"], new Dictionary<string, string>
            {
                ["conversationId"] = "number",
                ["role"] = "string",
                ["content"] = "string",
                ["metadata"] = "object"
            }),
            WorkflowNodeType.EditMessage => BuildObjectSchema(["conversationId", "messageId", "content"], new Dictionary<string, string>
            {
                ["conversationId"] = "number",
                ["messageId"] = "number",
                ["content"] = "string",
                ["metadata"] = "object"
            }),
            WorkflowNodeType.DeleteMessage => BuildObjectSchema(["userId", "conversationId", "messageId"], new Dictionary<string, string>
            {
                ["userId"] = "number",
                ["conversationId"] = "number",
                ["messageId"] = "number"
            }),
            WorkflowNodeType.Comment => BuildObjectSchema(["content"]),
            _ => BuildObjectSchema([])
        };
    }

    private static string BuildObjectSchema(IReadOnlyList<string> required, IReadOnlyDictionary<string, string>? properties = null)
    {
        var schemaProperties = properties ?? required.ToDictionary(key => key, _ => "string");
        var propertyBuilder = new StringBuilder();
        var index = 0;
        foreach (var property in schemaProperties)
        {
            if (index > 0)
            {
                propertyBuilder.AppendLine(",");
            }

            propertyBuilder.Append($"    \"{property.Key}\": {{ \"type\": \"{property.Value}\" }}");
            index++;
        }

        var requiredJson = required.Count == 0
            ? "[]"
            : $"[{string.Join(", ", required.Select(item => $"\"{item}\""))}]";

        return $$"""
                 {
                   "type": "object",
                   "properties": {
                 {{propertyBuilder}}
                   },
                   "required": {{requiredJson}},
                   "additionalProperties": true
                 }
                 """;
    }

    private static WorkflowNodePortMetadata In(string key, string dataType = "any", bool required = false, int maxConnections = 1)
        => new(key, key, WorkflowNodePortDirection.Input, dataType, required, maxConnections);

    private static WorkflowNodePortMetadata Out(string key, string dataType = "any", bool required = false, int maxConnections = 1)
        => new(key, key, WorkflowNodePortDirection.Output, dataType, required, maxConnections);
}
