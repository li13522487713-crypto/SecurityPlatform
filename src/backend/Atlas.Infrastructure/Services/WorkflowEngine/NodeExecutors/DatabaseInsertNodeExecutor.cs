using System.Text.Json;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 数据库新增节点：向 AiDatabaseRecord 批量新增记录。
/// D3：调用 AiDatabaseValueCoercer 强制类型化；D9：按数据库 QueryMode/ChannelScope 注入 owner/channel。
/// </summary>
public sealed class DatabaseInsertNodeExecutor : INodeExecutor
{
    private readonly ISqlSugarClient _db;

    public DatabaseInsertNodeExecutor(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _db = db;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.DatabaseInsert;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var databaseId = AiDatabaseNodeHelper.ResolveDatabaseId(context);
        if (databaseId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "DatabaseInsert 缺少 databaseInfoId/databaseId。");
        }

        using var activity = AiNodeObservability.StartNodeActivity(
            "AiDatabase.Insert",
            context.TenantId,
            context.UserId,
            context.ChannelId,
            context.Node.Key,
            new Dictionary<string, object?> { ["db.id"] = databaseId });

        var rowPayloads = ResolveRows(context.Node.Config);
        if (rowPayloads.Count == 0)
        {
            outputs["affected_rows"] = JsonSerializer.SerializeToElement(0);
            return new NodeExecutionResult(true, outputs);
        }

        var injectUserContext = context.GetConfigBoolean("injectUserContext", true);
        var policy = await AiDatabaseNodeHelper.ResolvePolicyAsync(_db, context, databaseId, cancellationToken);
        var ownerUserId = injectUserContext ? policy.OwnerUserId ?? context.UserId : null;
        var channelId = injectUserContext ? policy.ChannelId ?? context.ChannelId : null;
        var service = AiDatabaseNodeHelper.ResolveDatabaseService(context);
        var bulkResult = await service.CreateRecordsBulkAsync(
            context.TenantId,
            databaseId,
            new AiDatabaseRecordBulkCreateRequest(
                rowPayloads.Select(x => x.GetRawText()).ToArray(),
                AiDatabaseNodeHelper.ResolveEnvironment(context)),
            cancellationToken,
            ownerUserId,
            ownerUserId,
            channelId,
            enforceSyncBulkRowLimit: false);

        outputs["affected_rows"] = JsonSerializer.SerializeToElement(bulkResult.Succeeded);
        activity?.SetTag("db.affected_rows", bulkResult.Succeeded);
        await AiNodeObservability.WriteAuditAsync(
            context.ServiceProvider,
            context.TenantId,
            context.UserId,
            "ai_database_node.insert",
            "success",
            $"db:{databaseId}/rows:{bulkResult.Succeeded}/node:{context.Node.Key}",
            cancellationToken);
        return new NodeExecutionResult(true, outputs);
    }

    private static List<JsonElement> ResolveRows(IReadOnlyDictionary<string, JsonElement> config)
    {
        if (!VariableResolver.TryGetConfigValue(config, "rows", out var rowsRaw))
        {
            return [];
        }

        if (rowsRaw.ValueKind == JsonValueKind.Array)
        {
            return rowsRaw.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Object)
                .Select(x => x.Clone())
                .ToList();
        }

        return rowsRaw.ValueKind == JsonValueKind.Object
            ? [rowsRaw.Clone()]
            : [];
    }
}
