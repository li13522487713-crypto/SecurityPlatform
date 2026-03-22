using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

/// <summary>平台级权限仓储（应用级权限已物理分离至 IAppPermissionRepository）</summary>
public interface IPermissionRepository
{
    Task<Permission?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<Permission?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Permission> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        string? type,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Permission>> QueryByIdsAsync(TenantId tenantId, IReadOnlyList<long> ids, CancellationToken cancellationToken);
    Task<IReadOnlyList<Permission>> QueryAllAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task AddAsync(Permission permission, CancellationToken cancellationToken);
    Task UpdateAsync(Permission permission, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
