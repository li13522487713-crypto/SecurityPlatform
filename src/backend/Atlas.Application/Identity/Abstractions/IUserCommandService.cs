using Atlas.Core.Tenancy;
using Atlas.Application.Identity.Models;

namespace Atlas.Application.Identity.Abstractions;

public interface IUserCommandService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        UserCreateRequest request,
        long id,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long userId,
        UserUpdateRequest request,
        CancellationToken cancellationToken);

    Task UpdateRolesAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<long> roleIds,
        CancellationToken cancellationToken);

    Task UpdateDepartmentsAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<long> departmentIds,
        CancellationToken cancellationToken);

    Task UpdatePositionsAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<long> positionIds,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);
}
