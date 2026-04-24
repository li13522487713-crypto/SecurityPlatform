using System.Text.Json;
using Atlas.Core.Exceptions;
using Atlas.Domain.AiPlatform.Enums;
namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 数据库查询节点：记录以 JSON 存在 <see cref="Atlas.Domain.AiPlatform.Entities.AiDatabaseRecord.DataJson"/>。
/// SQL 层按租户 + 访问策略（Owner/Channel）过滤；clause 条件在应用层对 JSON 匹配；可通过 maxSqlScanRows 限制单次扫描行数。
/// </summary>
public sealed class DatabaseQueryNodeExecutor : INodeExecutor
{
    public DatabaseQueryNodeExecutor()
    {
    }

    public DatabaseQueryNodeExecutor(object? _ignored)
    {
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.DatabaseQuery;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputKey = context.GetConfigString("outputKey", "db_rows");
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

        try
        {
            var limit = Math.Clamp(context.GetConfigInt32("limit", 100), 1, 5000);
            var maxSqlScanRows = Math.Clamp(context.GetConfigInt32("maxSqlScanRows", 100_000), 100, 500_000);
            var queryFields = AiDatabaseNodeHelper.ResolveFields(context.Node.Config, "queryFields");
            var clauses = AiDatabaseNodeHelper.ResolveClauses(context.Node.Config);
            var records = await AiDatabaseNodeHelper.LoadRecordItemsAsync(
                context,
                databaseId,
                cancellationToken,
                maxSqlScanRows);

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

            var maskSensitive = context.GetConfigBoolean("maskSensitive", true);
            var emitted = maskSensitive ? AiNodeObservability.MaskAll(result) : result;
            outputs[outputKey] = JsonSerializer.SerializeToElement(emitted);
            outputs["record_count"] = JsonSerializer.SerializeToElement(emitted.Count);
            activity?.SetTag("db.records_returned", emitted.Count);
            activity?.SetTag("db.max_sql_scan_rows", maxSqlScanRows);

            await AiNodeObservability.WriteAuditAsync(
                context.ServiceProvider,
                context.TenantId,
                context.UserId,
                "ai_database_node.query",
                "success",
                $"db:{databaseId}/returned:{emitted.Count}/node:{context.Node.Key}",
                cancellationToken);

            return new NodeExecutionResult(true, outputs);
        }
        catch (Exception ex)
        {
            await AiNodeObservability.WriteAuditAsync(
                context.ServiceProvider,
                context.TenantId,
                context.UserId,
                "ai_database_node.query",
                "failure",
                $"db:{databaseId}/node:{context.Node.Key}",
                cancellationToken);
            return new NodeExecutionResult(false, outputs, ex is BusinessException bex ? bex.Message : "数据库查询失败。");
        }
    }
}
