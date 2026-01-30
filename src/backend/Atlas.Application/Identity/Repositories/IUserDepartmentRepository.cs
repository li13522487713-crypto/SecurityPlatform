using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IUserDepartmentRepository
{
    Task<IReadOnlyList<UserDepartment>> QueryByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken);
    Task DeleteByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken);
    Task DeleteByDepartmentIdAsync(TenantId tenantId, long departmentId, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<UserDepartment> userDepartments, CancellationToken cancellationToken);
}
