using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

/// <summary>
/// 计算字段绑定表达式服务（T02-21）—— 解析 computedExprId 求值。
/// 依赖 Track-03 T03-08 表达式引擎；当前版本为占位接口。
/// </summary>
public interface IComputedFieldBindingService
{
    Task<ComputedFieldBindingResult> EvaluateAsync(
        TenantId tenantId,
        string tableKey,
        string fieldName,
        IDictionary<string, object?> recordData,
        CancellationToken cancellationToken);
}
