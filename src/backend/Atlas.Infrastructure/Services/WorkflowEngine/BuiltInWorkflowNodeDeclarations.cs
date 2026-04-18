using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using System.Text;
using System.Text.Json;

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

    public string? FormMetaJson { get; init; }
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
        Create(WorkflowNodeType.DatabaseNl2Sql, "DatabaseNl2Sql", "自然语言查询", "database", "通过 LLM 把自然语言转为 JSON 查询计划，再走标准 DatabaseQuery 执行。", [In("input"), Out("output")], "#3B82F6"),

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
        Create(WorkflowNodeType.Comment, "Comment", "注释", "flow", "画布注释节点。", [In("input"), Out("output")], "#64748B"),

        // M12 触发器节点（PLAN.md §M12 S12-3 + docs/coze-node-mapping.md TriggerUpsert(34) / TriggerRead(35) / TriggerDelete(36)）
        Create(WorkflowNodeType.TriggerUpsert, "TriggerUpsert", "触发器创建/更新", "trigger", "创建或更新一个低代码触发器（CRON/事件/Webhook）。", [In("input"), Out("output")], "#22C55E"),
        Create(WorkflowNodeType.TriggerRead, "TriggerRead", "触发器读取", "trigger", "读取触发器列表或详情。", [In("input"), Out("output")], "#22C55E"),
        Create(WorkflowNodeType.TriggerDelete, "TriggerDelete", "触发器删除", "trigger", "删除指定触发器。", [In("input"), Out("output")], "#22C55E"),

        // M20 节点 49 全集补齐（PLAN.md §M20）
        // ── 上游对齐节点 ──
        Create(WorkflowNodeType.Variable, "Variable", "读变量", "data", "读取单一变量值（与 VariableAggregator 区分）。", [In("input"), Out("output")], "#06B6D4"),
        Create(WorkflowNodeType.ImageGenerate, "ImageGenerate", "图像生成（上游）", "image", "Coze 上游图像生成（ID 14）。", [In("input"), Out("output")], "#F472B6"),
        Create(WorkflowNodeType.Imageflow, "Imageflow", "图像流", "image", "Coze 上游 Imageflow（ID 15）。", [In("input"), Out("output")], "#F472B6"),
        Create(WorkflowNodeType.ImageReference, "ImageReference", "图像参考", "image", "Coze 上游 ImageReference（ID 16）。", [In("input"), Out("output")], "#F472B6"),
        Create(WorkflowNodeType.ImageCanvas, "ImageCanvasUpstream", "图像画布（上游）", "image", "Coze 上游 ImageCanvas（ID 17/23）。", [In("input"), Out("output")], "#F472B6"),
        Create(WorkflowNodeType.SceneVariable, "SceneVariable", "场景变量", "data", "Coze 上游场景变量（ID 24）。", [In("input"), Out("output")], "#06B6D4"),
        Create(WorkflowNodeType.SceneChat, "SceneChat", "场景对话", "ai", "Coze 上游场景对话（ID 25）。", [In("input"), Out("output")], "#8B5CF6"),
        Create(WorkflowNodeType.LtmUpstream, "LtmUpstream", "长期记忆（上游）", "knowledge", "Coze 上游长期记忆（ID 26）；与 Atlas 私有 Ltm(62) 联动。", [In("input"), Out("output")], "#10B981"),

        // ── 内存读写删（拆分自 Ltm）──
        Create(WorkflowNodeType.MemoryRead, "MemoryRead", "记忆读取", "knowledge", "按 key/userId 读取长期记忆。", [In("input"), Out("output")], "#10B981"),
        Create(WorkflowNodeType.MemoryWrite, "MemoryWrite", "记忆写入", "knowledge", "写入长期记忆。", [In("input"), Out("output")], "#10B981"),
        Create(WorkflowNodeType.MemoryDelete, "MemoryDelete", "记忆删除", "knowledge", "删除长期记忆。", [In("input"), Out("output")], "#10B981"),

        // ── Atlas 私有图像 N44/N45/N46 ──
        Create(WorkflowNodeType.ImageGeneration, "ImageGeneration", "图像生成（Atlas）", "image", "Atlas 私有图像生成节点（N44）。", [In("input"), Out("output")], "#FB7185"),
        Create(WorkflowNodeType.Canvas, "Canvas", "画布合成", "image", "Atlas 私有画布合成节点（N45）。", [In("input"), Out("output")], "#FB7185"),
        Create(WorkflowNodeType.ImagePlugin, "ImagePlugin", "图像插件", "image", "Atlas 私有图像插件节点（N46）。", [In("input"), Out("output")], "#FB7185"),

        // ── Atlas 私有视频 N47/N48/N49 ──
        Create(WorkflowNodeType.VideoGeneration, "VideoGeneration", "视频生成", "video", "Atlas 私有视频生成节点（N47）。", [In("input"), Out("output")], "#A855F7"),
        Create(WorkflowNodeType.VideoToAudio, "VideoToAudio", "视频转音频", "video", "Atlas 私有视频转音频节点（N48）。", [In("input"), Out("output")], "#A855F7"),
        Create(WorkflowNodeType.VideoFrameExtraction, "VideoFrameExtraction", "视频抽帧", "video", "Atlas 私有视频抽帧节点（N49）。", [In("input"), Out("output")], "#A855F7")
    ];

    private static readonly IReadOnlyDictionary<WorkflowNodeType, IReadOnlyList<WorkflowNodePortMetadata>> PortsByType =
        All.ToDictionary(x => x.Type, x => x.Ports);

    public static IReadOnlyList<WorkflowNodePortMetadata> GetPorts(WorkflowNodeType type)
    {
        return PortsByType.TryGetValue(type, out var ports)
            ? ports
            : [In("input"), Out("output")];
    }

    public static string GetDefaultInputPortKey(WorkflowNodeType type, string fallback = "input")
    {
        var port = GetPorts(type).FirstOrDefault(x => x.Direction == WorkflowNodePortDirection.Input);
        return port?.Key ?? fallback;
    }

    public static string GetDefaultOutputPortKey(WorkflowNodeType type, string fallback = "output")
    {
        var port = GetPorts(type).FirstOrDefault(x => x.Direction == WorkflowNodePortDirection.Output);
        return port?.Key ?? fallback;
    }

    public static Dictionary<string, JsonElement> GetDefaultConfig(WorkflowNodeType type)
    {
        return type switch
        {
            WorkflowNodeType.Entry => CreateConfig(
                ("entryVariable", "USER_INPUT"),
                ("entryDescription", string.Empty),
                ("entryAutoSaveHistory", true)),
            WorkflowNodeType.Exit => CreateConfig(
                ("exitTerminateMode", "return"),
                ("exitTemplate", "{{llm_output}}"),
                ("exitStreaming", false)),
            WorkflowNodeType.Selector => CreateConfig(
                ("condition", string.Empty),
                ("logic", "and"),
                ("conditions", Array.Empty<object>())),
            WorkflowNodeType.Loop => CreateConfig(
                ("mode", "forEach"),
                ("maxIterations", 10),
                ("collectionPath", "{{items}}"),
                ("condition", string.Empty),
                ("itemVariable", "loop_item"),
                ("itemIndexVariable", "loop_index"),
                ("bodyNodeKeys", string.Empty)),
            WorkflowNodeType.Batch => CreateConfig(
                ("concurrentSize", 4),
                ("batchSize", 10),
                ("inputArrayPath", "{{items}}"),
                ("itemVariable", "batch_item"),
                ("itemIndexVariable", "batch_item_index"),
                ("outputKey", "batch_results")),
            WorkflowNodeType.Break => CreateConfig(("reason", "manual_break")),
            WorkflowNodeType.Continue => CreateConfig(("remark", string.Empty)),
            WorkflowNodeType.Llm => CreateConfig(
                ("provider", string.Empty),
                ("model", string.Empty),
                ("prompt", "{{input.message}}"),
                ("systemPrompt", string.Empty),
                ("temperature", 0.7),
                ("maxTokens", 2048),
                ("stream", true),
                ("outputKey", "llm_output")),
            WorkflowNodeType.Agent => CreateConfig(
                ("agentId", string.Empty),
                ("message", "{{input.message}}"),
                ("conversationId", string.Empty),
                ("userId", string.Empty),
                ("enableRag", true),
                ("outputKey", "agent_output")),
            WorkflowNodeType.IntentDetector => CreateConfig(
                ("input", "{{input.message}}"),
                ("provider", string.Empty),
                ("model", string.Empty),
                ("systemPrompt", string.Empty),
                ("temperature", 0.2),
                ("intents", Array.Empty<object>())),
            WorkflowNodeType.QuestionAnswer => CreateConfig(
                ("question", string.Empty),
                ("answerType", "free_text"),
                ("fixedChoices", Array.Empty<object>()),
                ("maxAnswerCount", 1),
                ("answerPath", "answer")),
            WorkflowNodeType.CodeRunner => CreateConfig(
                ("language", "javascript"),
                ("code", string.Empty),
                ("outputKey", "code_output")),
            WorkflowNodeType.TextProcessor => CreateConfig(
                ("template", string.Empty),
                ("outputKey", "text_output")),
            WorkflowNodeType.JsonSerialization => CreateConfig(
                ("variableKeys", Array.Empty<object>()),
                ("outputKey", "json_output")),
            WorkflowNodeType.JsonDeserialization => CreateConfig(("inputVariable", string.Empty)),
            WorkflowNodeType.VariableAggregator => CreateConfig(
                ("variableKeys", Array.Empty<object>()),
                ("outputKey", "aggregated")),
            WorkflowNodeType.AssignVariable => CreateConfig(("assignments", string.Empty)),
            WorkflowNodeType.VariableAssignerWithinLoop => CreateConfig(("assignments", string.Empty)),
            WorkflowNodeType.Plugin => CreateConfig(
                ("pluginId", string.Empty),
                ("apiId", string.Empty),
                ("inputJson", new Dictionary<string, object?>()),
                ("outputKey", "plugin_output")),
            WorkflowNodeType.HttpRequester => CreateConfig(
                ("url", string.Empty),
                ("method", "GET"),
                ("headers", new Dictionary<string, string>()),
                ("body", new Dictionary<string, object?>()),
                ("timeout", 30),
                ("retryTimes", 1)),
            WorkflowNodeType.SubWorkflow => CreateConfig(
                ("workflowId", string.Empty),
                ("maxDepth", 5),
                ("inheritVariables", true),
                ("mergeOutputs", true),
                ("inputsVariable", string.Empty),
                ("outputKey", "sub_workflow_output")),
            WorkflowNodeType.InputReceiver => CreateConfig(
                ("inputPath", "input.message"),
                ("outputSchema", new Dictionary<string, object?>())),
            WorkflowNodeType.OutputEmitter => CreateConfig(
                ("outputKey", "output"),
                ("template", string.Empty)),
            WorkflowNodeType.KnowledgeRetriever => CreateConfig(
                ("knowledgeIds", Array.Empty<object>()),
                ("query", "{{input.message}}"),
                ("topK", 5),
                ("minScore", 0.2),
                ("tags", Array.Empty<object>()),
                ("ownerFilter", string.Empty)),
            WorkflowNodeType.KnowledgeIndexer => CreateConfig(
                ("knowledgeId", 0),
                ("fileId", 0),
                ("fileName", string.Empty),
                ("contentType", string.Empty),
                ("fileSizeBytes", 0),
                ("chunkSize", 500),
                ("overlap", 50),
                ("parseStrategy", "quick")),
            WorkflowNodeType.KnowledgeDeleter => CreateConfig(
                ("knowledgeId", 0),
                ("documentId", 0)),
            WorkflowNodeType.Ltm => CreateConfig(
                ("action", "query"),
                ("userId", 0),
                ("agentId", 0),
                ("conversationId", 0),
                ("memoryKey", string.Empty),
                ("content", string.Empty),
                ("source", "workflow"),
                ("limit", 10),
                ("memoryId", 0)),
            WorkflowNodeType.DatabaseQuery => CreateConfig(
                ("databaseInfoId", 0),
                ("queryFields", Array.Empty<object>()),
                ("clauseGroup", Array.Empty<object>()),
                ("limit", 100),
                ("outputKey", "db_rows")),
            WorkflowNodeType.DatabaseInsert => CreateConfig(
                ("databaseInfoId", 0),
                ("rows", Array.Empty<object>())),
            WorkflowNodeType.DatabaseUpdate => CreateConfig(
                ("databaseInfoId", 0),
                ("updateFields", new Dictionary<string, object?>()),
                ("clauseGroup", Array.Empty<object>())),
            WorkflowNodeType.DatabaseDelete => CreateConfig(
                ("databaseInfoId", 0),
                ("clauseGroup", Array.Empty<object>())),
            WorkflowNodeType.DatabaseCustomSql => CreateConfig(
                ("databaseInfoId", 0),
                ("sqlTemplate", string.Empty)),
            WorkflowNodeType.DatabaseNl2Sql => CreateConfig(
                ("databaseInfoId", 0),
                ("prompt", "{{input.message}}"),
                ("provider", string.Empty),
                ("model", string.Empty),
                ("limit", 100),
                ("outputKey", "db_rows")),
            WorkflowNodeType.CreateConversation => CreateConfig(
                ("title", string.Empty),
                ("userId", 0),
                ("agentId", 0)),
            WorkflowNodeType.ConversationList => CreateConfig(
                ("userId", 0),
                ("agentId", 0),
                ("pageIndex", 1),
                ("pageSize", 20)),
            WorkflowNodeType.ConversationUpdate => CreateConfig(
                ("userId", 0),
                ("conversationId", 0),
                ("title", string.Empty)),
            WorkflowNodeType.ConversationDelete => CreateConfig(
                ("userId", 0),
                ("conversationId", 0)),
            WorkflowNodeType.ConversationHistory => CreateConfig(
                ("userId", 0),
                ("conversationId", 0),
                ("limit", 20),
                ("includeContextMarkers", false)),
            WorkflowNodeType.ClearConversationHistory => CreateConfig(
                ("userId", 0),
                ("conversationId", 0)),
            WorkflowNodeType.MessageList => CreateConfig(
                ("userId", 0),
                ("conversationId", 0),
                ("pageIndex", 1),
                ("pageSize", 20)),
            WorkflowNodeType.CreateMessage => CreateConfig(
                ("conversationId", 0),
                ("role", "user"),
                ("content", "{{input.message}}"),
                ("metadata", new Dictionary<string, object?>())),
            WorkflowNodeType.EditMessage => CreateConfig(
                ("conversationId", 0),
                ("messageId", 0),
                ("content", "{{input.message}}"),
                ("metadata", new Dictionary<string, object?>())),
            WorkflowNodeType.DeleteMessage => CreateConfig(
                ("userId", 0),
                ("conversationId", 0),
                ("messageId", 0)),
            WorkflowNodeType.Comment => CreateConfig(("content", string.Empty)),
            _ => CreateConfig(("enabled", true))
        };
    }

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
                SupportsBatch: supportsBatch),
            FormMetaJson = BuildFormMeta(type)
        };
    }

    /// <summary>
    /// D9/K8：返回节点表单元数据 JSON（属性面板字段）。仅对 DB / KB / NL2SQL 节点提供详细 formMeta，
    /// 其余节点返回 null（前端按 ConfigSchemaJson 自动生成）。
    /// </summary>
    private static string? BuildFormMeta(WorkflowNodeType type)
    {
        return type switch
        {
            WorkflowNodeType.DatabaseQuery =>
                "[" +
                "{\"key\":\"databaseInfoId\",\"label\":\"数据库\",\"type\":\"ai_database_picker\",\"required\":true}," +
                "{\"key\":\"queryFields\",\"label\":\"查询字段\",\"type\":\"field_multiselect\",\"source\":\"databaseInfoId.fields\"}," +
                "{\"key\":\"clauseGroup\",\"label\":\"过滤条件\",\"type\":\"clause_group\",\"source\":\"databaseInfoId.fields\"}," +
                "{\"key\":\"limit\",\"label\":\"返回上限\",\"type\":\"number\",\"min\":1,\"max\":5000,\"default\":100}," +
                "{\"key\":\"outputKey\",\"label\":\"输出变量名\",\"type\":\"string\",\"default\":\"db_rows\"}" +
                "]",
            WorkflowNodeType.DatabaseInsert =>
                "[" +
                "{\"key\":\"databaseInfoId\",\"label\":\"数据库\",\"type\":\"ai_database_picker\",\"required\":true}," +
                "{\"key\":\"rows\",\"label\":\"插入行（JSON 数组或对象）\",\"type\":\"json_array\",\"source\":\"databaseInfoId.fields\"}," +
                "{\"key\":\"injectUserContext\",\"label\":\"注入当前用户/渠道\",\"type\":\"boolean\",\"default\":true,\"hint\":\"SingleUser/Channel 模式下自动写入 ownerUserId、channelId\"}" +
                "]",
            WorkflowNodeType.DatabaseUpdate =>
                "[" +
                "{\"key\":\"databaseInfoId\",\"label\":\"数据库\",\"type\":\"ai_database_picker\",\"required\":true}," +
                "{\"key\":\"updateFields\",\"label\":\"更新字段\",\"type\":\"field_value_map\",\"source\":\"databaseInfoId.fields\"}," +
                "{\"key\":\"clauseGroup\",\"label\":\"过滤条件\",\"type\":\"clause_group\",\"source\":\"databaseInfoId.fields\"}" +
                "]",
            WorkflowNodeType.DatabaseDelete =>
                "[" +
                "{\"key\":\"databaseInfoId\",\"label\":\"数据库\",\"type\":\"ai_database_picker\",\"required\":true}," +
                "{\"key\":\"clauseGroup\",\"label\":\"过滤条件\",\"type\":\"clause_group\",\"source\":\"databaseInfoId.fields\",\"required\":true}" +
                "]",
            WorkflowNodeType.DatabaseCustomSql =>
                "[" +
                "{\"key\":\"databaseInfoId\",\"label\":\"数据库\",\"type\":\"ai_database_picker\",\"required\":true}," +
                "{\"key\":\"sqlTemplate\",\"label\":\"SQL 模板\",\"type\":\"sql_editor\",\"required\":true,\"hint\":\"禁止 ;/--//* 与 DROP/TRUNCATE/ALTER\"}" +
                "]",
            WorkflowNodeType.DatabaseNl2Sql =>
                "[" +
                "{\"key\":\"databaseInfoId\",\"label\":\"数据库\",\"type\":\"ai_database_picker\",\"required\":true}," +
                "{\"key\":\"prompt\",\"label\":\"自然语言查询\",\"type\":\"string_template\",\"required\":true,\"default\":\"{{input.message}}\"}," +
                "{\"key\":\"provider\",\"label\":\"LLM Provider\",\"type\":\"string\"}," +
                "{\"key\":\"model\",\"label\":\"模型\",\"type\":\"string\",\"default\":\"gpt-4o-mini\"}," +
                "{\"key\":\"limit\",\"label\":\"返回上限\",\"type\":\"number\",\"min\":1,\"max\":5000,\"default\":100}," +
                "{\"key\":\"outputKey\",\"label\":\"输出变量名\",\"type\":\"string\",\"default\":\"db_rows\"}" +
                "]",
            WorkflowNodeType.KnowledgeRetriever =>
                "[" +
                "{\"key\":\"knowledgeIds\",\"label\":\"知识库\",\"type\":\"knowledge_picker\",\"multiple\":true,\"required\":true}," +
                "{\"key\":\"query\",\"label\":\"查询语句\",\"type\":\"string_template\",\"required\":true,\"default\":\"{{input.message}}\"}," +
                "{\"key\":\"topK\",\"label\":\"Top-K\",\"type\":\"number\",\"min\":1,\"max\":50,\"default\":5}," +
                "{\"key\":\"minScore\",\"label\":\"最低相似度\",\"type\":\"number\",\"min\":0,\"max\":1,\"step\":0.05,\"default\":0.2}," +
                "{\"key\":\"tags\",\"label\":\"标签过滤\",\"type\":\"tag_multiselect\"}," +
                "{\"key\":\"ownerFilter\",\"label\":\"Owner 过滤\",\"type\":\"string\",\"hint\":\"可选；非空时仅检索 owner=该值 的切片\"}" +
                "]",
            WorkflowNodeType.KnowledgeIndexer =>
                "[" +
                "{\"key\":\"knowledgeId\",\"label\":\"知识库\",\"type\":\"knowledge_picker\",\"required\":true}," +
                "{\"key\":\"fileId\",\"label\":\"文件 ID\",\"type\":\"file_picker\",\"required\":true}," +
                "{\"key\":\"fileName\",\"label\":\"文件名\",\"type\":\"string\"}," +
                "{\"key\":\"contentType\",\"label\":\"MIME 类型\",\"type\":\"string\"}," +
                "{\"key\":\"chunkSize\",\"label\":\"切片大小\",\"type\":\"number\",\"min\":50,\"max\":4000,\"default\":500}," +
                "{\"key\":\"overlap\",\"label\":\"切片重叠\",\"type\":\"number\",\"min\":0,\"max\":1000,\"default\":50}," +
                "{\"key\":\"parseStrategy\",\"label\":\"解析策略\",\"type\":\"select\",\"options\":[" +
                "{\"value\":\"quick\",\"label\":\"快速\"}," +
                "{\"value\":\"precise\",\"label\":\"精确\"}" +
                "],\"default\":\"quick\"}" +
                "]",
            WorkflowNodeType.KnowledgeDeleter =>
                "[" +
                "{\"key\":\"knowledgeId\",\"label\":\"知识库\",\"type\":\"knowledge_picker\",\"required\":true}," +
                "{\"key\":\"documentId\",\"label\":\"文档 ID\",\"type\":\"number\"}" +
                "]",
            _ => null
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
                ["minScore"] = "number",
                ["tags"] = "array",
                ["ownerFilter"] = "string"
            }),
            WorkflowNodeType.KnowledgeIndexer => BuildObjectSchema(["knowledgeId", "fileId"], new Dictionary<string, string>
            {
                ["knowledgeId"] = "number",
                ["fileId"] = "number",
                ["fileName"] = "string",
                ["contentType"] = "string",
                ["fileSizeBytes"] = "number",
                ["chunkSize"] = "number",
                ["overlap"] = "number",
                ["parseStrategy"] = "string"
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
            WorkflowNodeType.DatabaseNl2Sql => BuildObjectSchema(["databaseInfoId", "prompt"], new Dictionary<string, string>
            {
                ["databaseInfoId"] = "number",
                ["prompt"] = "string",
                ["provider"] = "string",
                ["model"] = "string",
                ["limit"] = "number",
                ["outputKey"] = "string"
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

    private static Dictionary<string, JsonElement> CreateConfig(params (string Key, object? Value)[] items)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in items)
        {
            result[key] = JsonSerializer.SerializeToElement(value);
        }

        return result;
    }

    private static WorkflowNodePortMetadata In(string key, string dataType = "any", bool required = false, int maxConnections = 1)
        => new(key, key, WorkflowNodePortDirection.Input, dataType, required, maxConnections);

    private static WorkflowNodePortMetadata Out(string key, string dataType = "any", bool required = false, int maxConnections = 1)
        => new(key, key, WorkflowNodePortDirection.Output, dataType, required, maxConnections);
}
