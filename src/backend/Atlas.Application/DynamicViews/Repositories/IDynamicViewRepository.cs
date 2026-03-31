using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicViews.Entities;

namespace Atlas.Application.DynamicViews.Repositories;

public interface IDynamicViewRepository
{
    Task<(IReadOnlyList<DynamicViewDefinition> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long? appId,
        PagedRequest request,
        CancellationToken cancellationToken);

    Task<DynamicViewDefinition?> FindByKeyAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken);

    Task AddAsync(DynamicViewDefinition entity, CancellationToken cancellationToken);

    Task UpdateAsync(DynamicViewDefinition entity, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long? appId, long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DynamicViewDefinition>> FindReferencingViewAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DynamicViewDefinition>> FindByTableReferenceAsync(
        TenantId tenantId,
        long? appId,
        string tableKey,
        CancellationToken cancellationToken);
}
