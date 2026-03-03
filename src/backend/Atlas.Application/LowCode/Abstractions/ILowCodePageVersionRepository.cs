using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Abstractions;

public interface ILowCodePageVersionRepository
{
    Task<LowCodePageVersion?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LowCodePageVersion>> GetByPageIdAsync(
        TenantId tenantId,
        long pageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LowCodePageVersion>> GetByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default);

    Task InsertAsync(LowCodePageVersion entity, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyList<LowCodePageVersion> entities, CancellationToken cancellationToken = default);

    Task DeleteByPageIdAsync(long pageId, CancellationToken cancellationToken = default);

    Task DeleteByAppIdAsync(long appId, CancellationToken cancellationToken = default);
}
