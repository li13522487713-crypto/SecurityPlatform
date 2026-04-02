using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 计算字段绑定服务（T02-21）—— 占位实现。
/// 依赖 Track-03 T03-08（AST 缓存/编译）就绪后联调表达式引擎。
/// </summary>
public sealed class ComputedFieldBindingService : IComputedFieldBindingService
{
    private readonly IDynamicTableRepository _tableRepo;
    private readonly IDynamicFieldRepository _fieldRepo;

    public ComputedFieldBindingService(
        IDynamicTableRepository tableRepo,
        IDynamicFieldRepository fieldRepo)
    {
        _tableRepo = tableRepo;
        _fieldRepo = fieldRepo;
    }

    public async Task<ComputedFieldBindingResult> EvaluateAsync(
        TenantId tenantId,
        string tableKey,
        string fieldName,
        IDictionary<string, object?> recordData,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepo.FindByKeyAsync(tenantId, tableKey, null, cancellationToken);
        if (table is null)
        {
            return new ComputedFieldBindingResult(tableKey, fieldName, 0, null,
                $"Table '{tableKey}' not found");
        }

        var fields = await _fieldRepo.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var field = fields.FirstOrDefault(f =>
            string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        if (field is null || !field.IsComputed || !field.ComputedExprId.HasValue)
        {
            return new ComputedFieldBindingResult(tableKey, fieldName, 0, null,
                $"Field '{fieldName}' is not a computed field or has no expression binding");
        }

        // 占位：Track-03 T03-08 AST 编译缓存就绪后，
        // 通过 IExpressionEvaluator.Evaluate(field.ComputedExprId.Value, recordData) 求值
        return new ComputedFieldBindingResult(tableKey, fieldName, field.ComputedExprId.Value, null,
            "Expression evaluation not yet implemented (pending Track-03)");
    }
}
