using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>
/// 应用版本管理（M14 S14-1..S14-2）。
///
/// 端点双套校准（PLAN.md §M14）：
///  - 设计态 v1（AppHost）：list / snapshot / diff / rollback —— 由 IAppDefinitionCommandService 已覆盖快照接口；diff/rollback 在本服务实现。
///  - 运行时 runtime（AppHost）：versions:archive / versions/{id}:rollback —— 由本服务 Archive / Rollback 实现。
/// </summary>
public interface IAppVersioningService
{
    Task<AppVersionDiffDto> DiffAsync(TenantId tenantId, long appId, long fromVersionId, long toVersionId, CancellationToken cancellationToken);
    Task RollbackAsync(TenantId tenantId, long currentUserId, long appId, long versionId, AppVersionRollbackRequest request, CancellationToken cancellationToken);
    /// <summary>运行时 archive：把"当前生效版本"快照新增一条版本归档。</summary>
    Task<long> ArchiveCurrentAsync(TenantId tenantId, long currentUserId, long appId, CancellationToken cancellationToken);
}

public interface IResourceReferenceGuardService
{
    /// <summary>检查资源是否被任意应用引用；返回引用列表（按 appId 聚合）。</summary>
    Task<IReadOnlyList<AppResourceReferenceDto>> ListByResourceAsync(TenantId tenantId, string resourceType, string resourceId, CancellationToken cancellationToken);
    /// <summary>删除前的阻断检查；若有引用则抛 BusinessException。</summary>
    Task EnsureCanDeleteAsync(TenantId tenantId, string resourceType, string resourceId, CancellationToken cancellationToken);
    /// <summary>把应用 schema 中的引用同步入索引（替换语义）。</summary>
    Task ReindexForAppAsync(TenantId tenantId, long appId, IReadOnlyList<AppResourceReferenceDto> references, CancellationToken cancellationToken);
}

public interface IAppFaqService
{
    Task<IReadOnlyList<AppFaqEntryDto>> SearchAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken);
    Task<long> UpsertAsync(TenantId tenantId, long currentUserId, AppFaqUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken);
    Task<AppFaqEntryDto?> HitAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
