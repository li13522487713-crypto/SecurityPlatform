using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

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
    Task AddAsync(Permission permission, CancellationToken cancellationToken);
    Task UpdateAsync(Permission permission, CancellationToken cancellationToken);
}
