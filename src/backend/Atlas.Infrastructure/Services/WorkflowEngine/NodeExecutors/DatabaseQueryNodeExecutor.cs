using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 数据库查询节点：当前版本为占位实现。
/// Config 参数：query、databaseId、outputKey
/// </summary>
public sealed class DatabaseQueryNodeExecutor : INodeExecutor
{
    private readonly ISqlSugarClient _db;

    public DatabaseQueryNodeExecutor(ISqlSugarClient db)
    {
        _db = db;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.DatabaseQuery;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputKey = context.GetConfigString("outputKey", "query_result");
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var databaseId = AiDatabaseNodeHelper.ResolveDatabaseId(context);
        if (databaseId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "DatabaseQuery 缺少 databaseInfoId/databaseId。");
        }

        using var activity = AiNodeObservability.StartNodeActivity(
            "AiDatabase.Query",
            context.TenantId,
            context.UserId,
            context.ChannelId,
            context.Node.Key,
            new Dictionary<string, object?> { ["db.id"] = databaseId });

        var limit = Math.Clamp(context.GetConfigInt32("limit", 100), 1, 5000);
        var queryFields = AiDatabaseNodeHelper.ResolveFields(context.Node.Config, "queryFields");
        var clauses = AiDatabaseNodeHelper.ResolveClauses(context.Node.Config);
        // D2：策略过滤（SingleUser/Channel 模式自动按当前 UserId/ChannelId 隔离）。
        var policy = await AiDatabaseNodeHelper.ResolvePolicyAsync(_db, context, databaseId, cancellationToken);
        var records = await AiDatabaseNodeHelper.LoadRecordsAsync(_db, context.TenantId, databaseId, cancellationToken, policy);

        var result = new List<JsonElement>();
        foreach (var record in records)
        {
            var payload = AiDatabaseNodeHelper.ParseRecordJson(record.DataJson);
            if (payload is null || !AiDatabaseNodeHelper.IsMatch(payload.Value, clauses))
            {
                continue;
            }

            result.Add(AiDatabaseNodeHelper.ApplyFieldProjection(payload.Value, queryFields));
            if (result.Count >= limit)
            {
                break;
            }
        }

        // X2：默认对查询结果应用敏感字段脱敏；可通过 config.maskSensitive=false 关闭。
        var maskSensitive = context.GetConfigBoolean("maskSensitive", true);
        var emitted = maskSensitive ? AiNodeObservability.MaskAll(result) : result;
        outputs[outputKey] = JsonSerializer.SerializeToElement(emitted);
        outputs["record_count"] = JsonSerializer.SerializeToElement(emitted.Count);
        activity?.SetTag("db.records_returned", emitted.Count);
        return new NodeExecutionResult(true, outputs);
    }
}
