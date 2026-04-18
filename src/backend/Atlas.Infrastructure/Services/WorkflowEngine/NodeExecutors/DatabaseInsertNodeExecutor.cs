using System.Text.Json;
using Atlas.Core.Abstractions;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.AiPlatform;
using SqlSugar;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 数据库新增节点：向 AiDatabaseRecord 批量新增记录。
/// D3：调用 AiDatabaseValueCoercer 强制类型化；D9：按数据库 QueryMode/ChannelScope 注入 owner/channel。
/// </summary>
public sealed class DatabaseInsertNodeExecutor : INodeExecutor
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public DatabaseInsertNodeExecutor(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _db = db;
        _idGeneratorAccessor = idGeneratorAccessor;
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

        var schemaJson = await AiDatabaseNodeHelper.LoadSchemaAsync(
            _db,
            context.TenantId,
            databaseId,
            cancellationToken,
            context.ServiceProvider);
        var entities = new List<AiDatabaseRecord>(rowPayloads.Count);
        foreach (var row in rowPayloads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var coerced = AiDatabaseValueCoercer.Coerce(schemaJson, row.GetRawText());
            entities.Add(new AiDatabaseRecord(
                context.TenantId,
                databaseId,
                coerced,
                _idGeneratorAccessor.NextId(),
                ownerUserId,
                ownerUserId,
                channelId));
        }

        await _db.Insertable(entities).ExecuteCommandAsync(cancellationToken);
        outputs["affected_rows"] = JsonSerializer.SerializeToElement(entities.Count);
        activity?.SetTag("db.affected_rows", entities.Count);
        await AiNodeObservability.WriteAuditAsync(
            context.ServiceProvider,
            context.TenantId,
            context.UserId,
            "ai_database_node.insert",
            "success",
            $"db:{databaseId}/rows:{entities.Count}/node:{context.Node.Key}",
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
