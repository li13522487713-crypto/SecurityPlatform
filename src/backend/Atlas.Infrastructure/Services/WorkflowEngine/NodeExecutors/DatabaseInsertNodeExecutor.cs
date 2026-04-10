using System.Text.Json;
using Atlas.Core.Abstractions;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 数据库新增节点：向 AiDatabaseRecord 批量新增记录。
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

        var rowPayloads = ResolveRows(context.Node.Config);
        if (rowPayloads.Count == 0)
        {
            outputs["affected_rows"] = JsonSerializer.SerializeToElement(0);
            return new NodeExecutionResult(true, outputs);
        }

        var entities = rowPayloads.Select(row => new AiDatabaseRecord(
                context.TenantId,
                databaseId,
                row.GetRawText(),
                _idGeneratorAccessor.NextId()))
            .ToArray();

        await _db.Insertable(entities).ExecuteCommandAsync(cancellationToken);
        outputs["affected_rows"] = JsonSerializer.SerializeToElement(entities.Length);
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
