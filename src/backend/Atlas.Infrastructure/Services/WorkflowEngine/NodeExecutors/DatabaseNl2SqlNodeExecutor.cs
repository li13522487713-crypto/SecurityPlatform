using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.AiPlatform;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// D6：NL2SQL 节点。把自然语言查询通过 LLM 转换为 JSON 形式的 clauseGroup + queryFields，
/// 再走标准的 <see cref="DatabaseQueryNodeExecutor"/> 路径执行（不做原始 SQL，避免注入与高危操作）。
///
/// Config:
///  - databaseInfoId: 目标 AiDatabase id（必填）
///  - prompt:         自然语言查询模板（默认 {{input.message}}）
///  - provider/model: LLM provider/model（沿用 LlmNodeExecutor 约定）
///  - limit:          可选，1-5000，默认 100
///  - outputKey:      默认 "db_rows"
/// </summary>
public sealed class DatabaseNl2SqlNodeExecutor : INodeExecutor
{
    private readonly ISqlSugarClient _db;
    private readonly ILlmProviderFactory _llmProviderFactory;
    private readonly DatabaseQueryNodeExecutor _queryExecutor;
    private readonly ILogger<DatabaseNl2SqlNodeExecutor> _logger;

    public DatabaseNl2SqlNodeExecutor(
        ISqlSugarClient db,
        ILlmProviderFactory llmProviderFactory,
        ILogger<DatabaseNl2SqlNodeExecutor> logger)
    {
        _db = db;
        _llmProviderFactory = llmProviderFactory;
        _queryExecutor = new DatabaseQueryNodeExecutor(db);
        _logger = logger;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.DatabaseNl2Sql;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var databaseId = AiDatabaseNodeHelper.ResolveDatabaseId(context);
        if (databaseId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "DatabaseNl2Sql 缺少 databaseInfoId/databaseId。");
        }

        var promptTemplate = context.GetConfigString("prompt", "{{input.message}}");
        var question = context.ReplaceVariables(promptTemplate).Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            return new NodeExecutionResult(false, outputs, "DatabaseNl2Sql 自然语言查询为空。");
        }

        var schemaJson = await AiDatabaseNodeHelper.LoadSchemaAsync(_db, context.TenantId, databaseId, cancellationToken);
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return new NodeExecutionResult(false, outputs, "DatabaseNl2Sql 数据库 schema 为空。");
        }

        var columns = AiDatabaseValueCoercer.ParseColumns(schemaJson);
        if (columns.Count == 0)
        {
            return new NodeExecutionResult(false, outputs, "DatabaseNl2Sql 数据库无可用字段。");
        }

        // ── 1. 调 LLM 生成 JSON 计划 ──
        DatabaseNl2SqlPlan? plan;
        try
        {
            plan = await GeneratePlanAsync(context, columns, question, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DatabaseNl2Sql LLM 调用失败 db={DatabaseId}", databaseId);
            return new NodeExecutionResult(false, outputs, $"DatabaseNl2Sql LLM 调用失败: {ex.Message}");
        }

        if (plan is null)
        {
            return new NodeExecutionResult(false, outputs, "DatabaseNl2Sql 未生成可用查询计划。");
        }

        // ── 2. 注入到 config，复用 DatabaseQueryNodeExecutor 执行 ──
        var enrichedConfig = new Dictionary<string, JsonElement>(context.Node.Config, StringComparer.OrdinalIgnoreCase)
        {
            ["clauseGroup"] = JsonSerializer.SerializeToElement(plan.Clauses),
            ["queryFields"] = JsonSerializer.SerializeToElement(plan.Fields)
        };
        if (plan.Limit is { } limit)
        {
            enrichedConfig["limit"] = JsonSerializer.SerializeToElement(limit);
        }

        var virtualNode = context.Node with
        {
            Type = WorkflowNodeType.DatabaseQuery,
            Config = enrichedConfig
        };
        var queryContext = new NodeExecutionContext(
            virtualNode,
            new Dictionary<string, JsonElement>(context.Variables, StringComparer.OrdinalIgnoreCase),
            context.ServiceProvider,
            context.TenantId,
            context.WorkflowId,
            context.ExecutionId,
            context.WorkflowCallStack,
            context.EventChannel,
            context.UserId,
            context.ChannelId);
        var queryResult = await _queryExecutor.ExecuteAsync(queryContext, cancellationToken);

        // ── 3. 透出 plan 供调试/观测 ──
        var merged = new Dictionary<string, JsonElement>(queryResult.Outputs, StringComparer.OrdinalIgnoreCase)
        {
            ["nl2sql_plan"] = JsonSerializer.SerializeToElement(plan),
            ["nl2sql_question"] = VariableResolver.CreateStringElement(question)
        };
        return new NodeExecutionResult(queryResult.Success, merged, queryResult.ErrorMessage, queryResult.InterruptType);
    }

    private async Task<DatabaseNl2SqlPlan?> GeneratePlanAsync(
        NodeExecutionContext context,
        IReadOnlyList<AiDatabaseColumnDefinition> columns,
        string question,
        CancellationToken cancellationToken)
    {
        var provider = context.GetConfigString("provider");
        var model = context.GetConfigString("model.modelName", context.GetConfigString("model", "gpt-4o-mini"));
        var llm = _llmProviderFactory.GetLlmProvider(provider);

        var schemaSummary = string.Join(
            "\n",
            columns.Select(c => $"- {c.Name} ({c.Type.ToString().ToLowerInvariant()}{(c.Required ? ", required" : string.Empty)})"));
        var systemPrompt =
            "你是一个把自然语言查询翻译为 JSON 查询计划的助手。" +
            "只能从给定的列里选字段；运算符 op ∈ {eq,ne,gt,lt,ge,le,contains}；logic ∈ {and,or}。" +
            "返回严格 JSON：{\"fields\":[..],\"clauses\":[{\"field\":\"..\",\"op\":\"..\",\"value\":\"..\",\"logic\":\"..\"}],\"limit\":N}。" +
            "若无法解析，返回 {\"fields\":[],\"clauses\":[],\"limit\":null}。不得添加额外文本。";
        var userPrompt = new StringBuilder()
            .AppendLine("数据库列：")
            .AppendLine(schemaSummary)
            .AppendLine()
            .Append("自然语言查询：").Append(question).AppendLine()
            .AppendLine("返回 JSON：")
            .ToString();

        var request = new ChatCompletionRequest(
            model,
            new List<ChatMessage>
            {
                new("system", systemPrompt),
                new("user", userPrompt)
            },
            Temperature: 0.0f,
            MaxTokens: 1024,
            Provider: provider);
        var result = await llm.ChatAsync(request, cancellationToken);
        var raw = result.Content;

        var jsonText = ExtractJsonObject(raw);
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;
            var fields = ReadStringArray(root, "fields");
            var clauses = ReadClauses(root);
            int? limit = root.TryGetProperty("limit", out var limitNode) && limitNode.ValueKind == JsonValueKind.Number
                ? Math.Clamp(limitNode.GetInt32(), 1, 5000)
                : null;
            return new DatabaseNl2SqlPlan(fields, clauses, limit);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "DatabaseNl2Sql 计划 JSON 解析失败：{Raw}", raw);
            return null;
        }
    }

    private static string ExtractJsonObject(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline > 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }
            var fenceEnd = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceEnd > 0)
            {
                trimmed = trimmed[..fenceEnd];
            }
        }
        var openIdx = trimmed.IndexOf('{');
        var closeIdx = trimmed.LastIndexOf('}');
        if (openIdx < 0 || closeIdx <= openIdx)
        {
            return string.Empty;
        }
        return trimmed.Substring(openIdx, closeIdx - openIdx + 1);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var node) || node.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }
        var list = new List<string>();
        foreach (var item in node.EnumerateArray())
        {
            var text = VariableResolver.ToDisplayText(item);
            if (!string.IsNullOrWhiteSpace(text))
            {
                list.Add(text.Trim());
            }
        }
        return list;
    }

    private static IReadOnlyList<DatabaseNl2SqlClause> ReadClauses(JsonElement root)
    {
        if (!root.TryGetProperty("clauses", out var node) || node.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<DatabaseNl2SqlClause>();
        }
        var list = new List<DatabaseNl2SqlClause>();
        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var field = item.TryGetProperty("field", out var f) ? VariableResolver.ToDisplayText(f).Trim() : string.Empty;
            var op = item.TryGetProperty("op", out var o) ? VariableResolver.ToDisplayText(o).Trim() : string.Empty;
            var value = item.TryGetProperty("value", out var v) ? VariableResolver.ToDisplayText(v) : string.Empty;
            var logic = item.TryGetProperty("logic", out var l) ? VariableResolver.ToDisplayText(l).Trim() : "and";
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op))
            {
                continue;
            }
            list.Add(new DatabaseNl2SqlClause(field, op, value, string.IsNullOrWhiteSpace(logic) ? "and" : logic));
        }
        return list;
    }
}

/// <summary>D6：NL2SQL 计划。</summary>
public sealed record DatabaseNl2SqlPlan(
    IReadOnlyList<string> Fields,
    IReadOnlyList<DatabaseNl2SqlClause> Clauses,
    int? Limit);

public sealed record DatabaseNl2SqlClause(string Field, string Op, string Value, string Logic);
