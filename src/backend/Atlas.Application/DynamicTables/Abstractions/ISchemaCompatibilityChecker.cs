using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

/// <summary>
/// Schema 兼容性检查入口 —— 聚合名称冲突、类型兼容、索引/外键影响、
/// 函数依赖影响、逻辑流依赖影响 5 类检测，并输出高风险变更预警。
/// </summary>
public interface ISchemaCompatibilityChecker
{
    Task<SchemaCompatibilityResult> CheckAsync(
        TenantId tenantId,
        SchemaCompatibilityCheckRequest request,
        CancellationToken cancellationToken);
}
