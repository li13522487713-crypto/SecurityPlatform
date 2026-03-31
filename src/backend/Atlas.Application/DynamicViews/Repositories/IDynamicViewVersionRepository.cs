using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicViews.Entities;

namespace Atlas.Application.DynamicViews.Repositories;

public interface IDynamicViewVersionRepository
{
    Task<int> GetLatestVersionAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken);

    Task AddAsync(DynamicViewVersion entity, CancellationToken cancellationToken);

    Task<IReadOnlyList<DynamicViewVersion>> ListByViewKeyAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken);

    Task<DynamicViewVersion?> FindByVersionAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        int version,
        CancellationToken cancellationToken);
}
