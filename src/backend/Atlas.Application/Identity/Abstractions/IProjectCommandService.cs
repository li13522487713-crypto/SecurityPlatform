using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IProjectCommandService
{
    Task<long> CreateAsync(TenantId tenantId, ProjectCreateRequest request, long id, CancellationToken cancellationToken);
    Task UpdateAsync(TenantId tenantId, long id, ProjectUpdateRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task UpdateUsersAsync(TenantId tenantId, long id, IReadOnlyList<long> userIds, CancellationToken cancellationToken);
    Task UpdateDepartmentsAsync(TenantId tenantId, long id, IReadOnlyList<long> departmentIds, CancellationToken cancellationToken);
    Task UpdatePositionsAsync(TenantId tenantId, long id, IReadOnlyList<long> positionIds, CancellationToken cancellationToken);
}
