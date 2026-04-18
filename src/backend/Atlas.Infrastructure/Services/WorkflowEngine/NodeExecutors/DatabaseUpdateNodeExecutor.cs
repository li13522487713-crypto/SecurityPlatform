using System.Text.Json;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 数据库更新节点：按条件更新 JSON 记录字段。
/// </summary>
public sealed class DatabaseUpdateNodeExecutor : INodeExecutor
{
    private readonly ISqlSugarClient _db;

    public DatabaseUpdateNodeExecutor(ISqlSugarClient db)
    {
        _db = db;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.DatabaseUpdate;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var databaseId = AiDatabaseNodeHelper.ResolveDatabaseId(context);
        if (databaseId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "DatabaseUpdate 缺少 databaseInfoId/databaseId。");
        }

        using var activity = AiNodeObservability.StartNodeActivity(
            "AiDatabase.Update",
            context.TenantId,
            context.UserId,
            context.ChannelId,
            context.Node.Key,
            new Dictionary<string, object?> { ["db.id"] = databaseId });

        if (!VariableResolver.TryGetConfigValue(context.Node.Config, "updateFields", out var updateFieldsRaw) ||
            updateFieldsRaw.ValueKind != JsonValueKind.Object)
        {
            return new NodeExecutionResult(false, outputs, "DatabaseUpdate 缺少 updateFields。");
        }

        var updateFields = updateFieldsRaw.EnumerateObject()
            .ToDictionary(x => x.Name, x => x.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        var clauses = AiDatabaseNodeHelper.ResolveClauses(context.Node.Config);
        var policy = await AiDatabaseNodeHelper.ResolvePolicyAsync(_db, context, databaseId, cancellationToken);
        var records = await AiDatabaseNodeHelper.LoadRecordsAsync(_db, context.TenantId, databaseId, cancellationToken, policy);
        var touched = new List<AiDatabaseRecord>();

        foreach (var record in records)
        {
            var payload = AiDatabaseNodeHelper.ParseRecordJson(record.DataJson);
            if (payload is null || !AiDatabaseNodeHelper.IsMatch(payload.Value, clauses))
            {
                continue;
            }

            var mergedJson = AiDatabaseNodeHelper.MergeObjectJson(payload.Value, updateFields);
            record.UpdateData(mergedJson);
            touched.Add(record);
        }

        if (touched.Count > 0)
        {
            await _db.Updateable(touched)
                .WhereColumns(x => new { x.Id, x.TenantIdValue })
                .ExecuteCommandAsync(cancellationToken);
        }

        outputs["affected_rows"] = JsonSerializer.SerializeToElement(touched.Count);
        activity?.SetTag("db.affected_rows", touched.Count);
        await AiNodeObservability.WriteAuditAsync(
            context.ServiceProvider,
            context.TenantId,
            context.UserId,
            "ai_database_node.update",
            "success",
            $"db:{databaseId}/rows:{touched.Count}/node:{context.Node.Key}",
            cancellationToken);
        return new NodeExecutionResult(true, outputs);
    }
}
