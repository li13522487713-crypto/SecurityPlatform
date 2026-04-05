using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IUserDepartmentRepository
{
    Task<IReadOnlyList<long>> QueryUserIdsByDepartmentIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> departmentIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UserDepartment>> QueryByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserDepartment>> QueryByUserIdsAsync(TenantId tenantId, IReadOnlyList<long> userIds, CancellationToken cancellationToken);
    Task DeleteByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken);
    Task DeleteByUserAndDepartmentIdsAsync(TenantId tenantId, long userId, IReadOnlyList<long> departmentIds, CancellationToken cancellationToken);
    Task DeleteByDepartmentIdAsync(TenantId tenantId, long departmentId, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<UserDepartment> userDepartments, CancellationToken cancellationToken);
}
