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

    Task<IReadOnlyDictionary<string, long>> CreateBatchAsync(
        TenantId tenantId,
        IReadOnlyList<UserBatchCreateItem> items,
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

    Task ChangePasswordAsync(
        TenantId tenantId,
        long userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);

    Task ResetPasswordAsync(
        TenantId tenantId,
        long userId,
        string newPassword,
        CancellationToken cancellationToken);

    Task UpdateProfileAsync(
        TenantId tenantId,
        long userId,
        string displayName,
        string? email,
        string? phoneNumber,
        CancellationToken cancellationToken);

    Task UpdateBasicInfoAsync(
        TenantId tenantId,
        long userId,
        string displayName,
        string? email,
        string? phoneNumber,
        bool isActive,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);
}
