using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IProjectDepartmentRepository
{
    Task<IReadOnlyList<ProjectDepartment>> QueryByProjectIdAsync(
        TenantId tenantId,
        long projectId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> QueryProjectIdsByDepartmentIdAsync(
        TenantId tenantId,
        long departmentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<long>> QueryProjectIdsByDepartmentIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> departmentIds,
        CancellationToken cancellationToken);
    Task DeleteByProjectIdAsync(TenantId tenantId, long projectId, CancellationToken cancellationToken);
    Task DeleteByDepartmentIdAsync(TenantId tenantId, long departmentId, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<ProjectDepartment> entities, CancellationToken cancellationToken);
}
