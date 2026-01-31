using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IDepartmentRepository
{
    Task<Department?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Department> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken);
    Task<(IReadOnlyList<Department> Items, int TotalCount)> QueryPageByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Department>> QueryAllAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Department>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken);
    Task AddAsync(Department department, CancellationToken cancellationToken);
    Task UpdateAsync(Department department, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<bool> ExistsByParentIdAsync(TenantId tenantId, long parentId, CancellationToken cancellationToken);
}
