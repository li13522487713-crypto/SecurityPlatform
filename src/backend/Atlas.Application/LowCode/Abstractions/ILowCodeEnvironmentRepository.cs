using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Abstractions;

public interface ILowCodeEnvironmentRepository
{
    Task<LowCodeEnvironment?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);

    Task<LowCodeEnvironment?> GetByCodeAsync(
        TenantId tenantId,
        long appId,
        string code,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LowCodeEnvironment>> GetByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default);

    Task InsertAsync(LowCodeEnvironment entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(LowCodeEnvironment entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(
        TenantId tenantId,
        long appId,
        string code,
        long? excludeId = null,
        CancellationToken cancellationToken = default);

    Task ClearDefaultByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default);
}
