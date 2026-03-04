using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Abstractions;

public interface ILowCodeAppVersionRepository
{
    Task<LowCodeAppVersion?> GetByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<LowCodeAppVersion> Items, int Total)> GetPagedAsync(
        TenantId tenantId,
        long appId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> GetLatestVersionAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default);

    Task InsertAsync(LowCodeAppVersion entity, CancellationToken cancellationToken = default);
}
