using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Abstractions;

public interface ILowCodePageRepository
{
    Task<LowCodePage?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<LowCodePage?> GetByKeyAsync(TenantId tenantId, long appId, string pageKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LowCodePage>> GetByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LowCodePage>> GetPublishedPagesAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task InsertAsync(LowCodePage entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(LowCodePage entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task DeleteByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByKeyAsync(TenantId tenantId, long appId, string pageKey, long? excludeId = null, CancellationToken cancellationToken = default);
}
