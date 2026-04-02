using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

/// <summary>
/// Expand / Migrate / Contract 三阶段迁移服务（T02-14 ~ T02-16, T02-29）
/// </summary>
public interface IExpandMigrateContractService
{
    Task<ExpandMigrateContractTask> CreateTaskAsync(
        TenantId tenantId,
        long userId,
        SchemaCompatibilityCheckRequest request,
        CancellationToken cancellationToken);

    /// <summary>T02-15: Expand 阶段 — 添加新字段/表/索引</summary>
    Task<MigrationPhaseResult> ExecuteExpandAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken);

    /// <summary>T02-29: Migrate 阶段 — 回填/双写/视图兼容</summary>
    Task<MigrationPhaseResult> ExecuteMigrateAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken);

    /// <summary>T02-16: Contract 阶段 — 下线旧字段/索引</summary>
    Task<MigrationPhaseResult> ExecuteContractAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken);
}
