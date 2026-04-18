using System.Text.Json;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 数据库删除节点：按条件删除记录。
/// </summary>
public sealed class DatabaseDeleteNodeExecutor : INodeExecutor
{
    private readonly ISqlSugarClient _db;

    public DatabaseDeleteNodeExecutor(ISqlSugarClient db)
    {
        _db = db;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.DatabaseDelete;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var databaseId = AiDatabaseNodeHelper.ResolveDatabaseId(context);
        if (databaseId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "DatabaseDelete 缺少 databaseInfoId/databaseId。");
        }

        using var activity = AiNodeObservability.StartNodeActivity(
            "AiDatabase.Delete",
            context.TenantId,
            context.UserId,
            context.ChannelId,
            context.Node.Key,
            new Dictionary<string, object?> { ["db.id"] = databaseId });

        var clauses = AiDatabaseNodeHelper.ResolveClauses(context.Node.Config);
        var policy = await AiDatabaseNodeHelper.ResolvePolicyAsync(_db, context, databaseId, cancellationToken);
        var records = await AiDatabaseNodeHelper.LoadRecordsAsync(_db, context.TenantId, databaseId, cancellationToken, policy);
        var deleteIds = records
            .Where(record =>
            {
                var payload = AiDatabaseNodeHelper.ParseRecordJson(record.DataJson);
                return payload is not null && AiDatabaseNodeHelper.IsMatch(payload.Value, clauses);
            })
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        var affectedRows = 0;
        if (deleteIds.Length > 0)
        {
            affectedRows = await _db.Deleteable<AiDatabaseRecord>()
                .Where(x =>
                    x.TenantIdValue == context.TenantId.Value &&
                    x.DatabaseId == databaseId &&
                    SqlFunc.ContainsArray(deleteIds, x.Id))
                .ExecuteCommandAsync(cancellationToken);
        }

        outputs["affected_rows"] = JsonSerializer.SerializeToElement(affectedRows);
        activity?.SetTag("db.affected_rows", affectedRows);
        await AiNodeObservability.WriteAuditAsync(
            context.ServiceProvider,
            context.TenantId,
            context.UserId,
            "ai_database_node.delete",
            "success",
            $"db:{databaseId}/rows:{affectedRows}/node:{context.Node.Key}",
            cancellationToken);
        return new NodeExecutionResult(true, outputs);
    }
}
