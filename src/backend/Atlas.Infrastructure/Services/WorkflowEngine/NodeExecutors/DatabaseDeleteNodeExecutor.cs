using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 数据库删除节点：按条件删除记录。
/// </summary>
public sealed class DatabaseDeleteNodeExecutor : INodeExecutor
{
    public DatabaseDeleteNodeExecutor()
    {
    }

    public DatabaseDeleteNodeExecutor(object? _ignored)
    {
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
        var records = await AiDatabaseNodeHelper.LoadRecordItemsAsync(
            context,
            databaseId,
            cancellationToken,
            500_000);
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
            var service = AiDatabaseNodeHelper.ResolveDatabaseService(context);
            foreach (var deleteId in deleteIds)
            {
                await service.DeleteRecordAsync(
                    context.TenantId,
                    databaseId,
                    deleteId,
                    AiDatabaseNodeHelper.ResolveEnvironment(context),
                    cancellationToken,
                    context.UserId,
                    context.ChannelId);
                affectedRows++;
            }
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
