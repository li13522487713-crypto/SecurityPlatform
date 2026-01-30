using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IDepartmentCommandService
{
    Task<long> CreateAsync(TenantId tenantId, DepartmentCreateRequest request, long id, CancellationToken cancellationToken);
    Task UpdateAsync(TenantId tenantId, long departmentId, DepartmentUpdateRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long departmentId, CancellationToken cancellationToken);
}
