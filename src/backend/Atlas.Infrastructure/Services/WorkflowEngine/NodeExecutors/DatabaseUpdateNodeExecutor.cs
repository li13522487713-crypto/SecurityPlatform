using System.Text.Json;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 数据库更新节点：按条件更新 JSON 记录字段。
/// </summary>
public sealed class DatabaseUpdateNodeExecutor : INodeExecutor
{
    public DatabaseUpdateNodeExecutor()
    {
    }

    public DatabaseUpdateNodeExecutor(object? _ignored)
    {
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
        var records = await AiDatabaseNodeHelper.LoadRecordItemsAsync(
            context,
            databaseId,
            cancellationToken,
            500_000);
        var service = AiDatabaseNodeHelper.ResolveDatabaseService(context);
        var touched = new List<(AiDatabaseRecordListItem Record, string DataJson)>();

        foreach (var record in records)
        {
            var payload = AiDatabaseNodeHelper.ParseRecordJson(record.DataJson);
            if (payload is null || !AiDatabaseNodeHelper.IsMatch(payload.Value, clauses))
            {
                continue;
            }

            var mergedJson = AiDatabaseNodeHelper.MergeObjectJson(payload.Value, updateFields);
            touched.Add((record, mergedJson));
        }

        if (touched.Count > 0)
        {
            foreach (var (record, dataJson) in touched)
            {
                await service.UpdateRecordAsync(
                    context.TenantId,
                    databaseId,
                    record.Id,
                    new AiDatabaseRecordUpdateRequest(dataJson, AiDatabaseNodeHelper.ResolveEnvironment(context)),
                    cancellationToken,
                    context.UserId,
                    context.ChannelId);
            }
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
