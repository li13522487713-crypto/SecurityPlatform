using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface ILowCodeEnvironmentService
{
    Task<IReadOnlyList<LowCodeEnvironmentListItem>> GetByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default);

    Task<LowCodeEnvironmentDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default);

    Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        long appId,
        LowCodeEnvironmentCreateRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        TenantId tenantId,
        long userId,
        long id,
        LowCodeEnvironmentUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default);
}
